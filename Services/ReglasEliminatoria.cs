using FairPlay.Models;
using FairPlay.Models.Enums;

namespace FairPlay.Services;

/// <summary>
/// Resultado de evaluar un cruce de eliminatoria con los partidos que tiene ahora.
///   Completo = todos sus partidos están Finalizado (1 partido, o 2 si es ida y vuelta).
///   Empate   = está completo, el marcador (global) está empatado y NO hay penales válidos todavía.
///   IdGanador = quién avanza (null si no está completo o si falta definir por penales).
///   GolesA / GolesB = goles acumulados de cada equipo del cruce (en ida y vuelta, el global).
/// </summary>
public record DecisionCruce(bool Completo, bool Empate, int? IdGanador, int GolesA, int GolesB);

/// <summary>
/// Reglas de la eliminatoria (cruces, penales y avance de ganadores) que comparten los servicios, el
/// controlador y las vistas. Igual que ReglasPartido o ReglasEvento: son estáticas y puras (no consultan la
/// base de datos), para que la pantalla no ofrezca lo que el servicio rechazaría. El servicio SIEMPRE vuelve
/// a comprobarlas, porque un POST puede enviarse a mano.
///
/// Reglas decididas para la Copa:
///   - La final es a partido único aunque el torneo sea de ida y vuelta.
///   - Un empate (en el partido único, o en el GLOBAL del partido de vuelta) se decide por penales.
///   - Los penales se guardan en penales_local / penales_visitante del partido decisivo; NO suman al marcador
///     ni son eventos.
///   - No se puede finalizar el partido que cierra un cruce mientras siga empatado sin penales.
/// </summary>
public static class ReglasEliminatoria
{
    /// <summary>Máximo de penales por equipo que acepta el formulario (evita valores absurdos).</summary>
    public const int MaxPenales = 30;

    // ------------------------------------------------------------------
    // Estructura del cruce
    // ------------------------------------------------------------------

    /// <summary>
    /// ¿El cruce se juega a ida y vuelta? Solo si la fase es de ida y vuelta Y el cruce no es la final
    /// (la final es el único cruce sin "cruce siguiente").
    /// </summary>
    public static bool EsIdaYVuelta(bool faseIdaYVuelta, Cruce cruce) =>
        faseIdaYVuelta && cruce.IdCruceSiguiente.HasValue;

    /// <summary>
    /// Crea los partidos NUEVOS de un cruce cuyos dos equipos ya se conocen (sin guardarlos; quien llama
    /// asigna la ronda y el cruce). Partido único: local = equipo A. Ida y vuelta: la ida la juega de local
    /// el equipo A y la vuelta el equipo B.
    /// </summary>
    public static List<Partido> PartidosDelCruce(int idEquipoA, int idEquipoB, bool dosPartidos)
    {
        var partidos = new List<Partido>
        {
            new Partido { NumeroPartido = 1, IdLocal = idEquipoA, IdVisitante = idEquipoB, Estado = EstadoPartido.Programado }
        };

        if (dosPartidos)
        {
            partidos.Add(new Partido { NumeroPartido = 2, IdLocal = idEquipoB, IdVisitante = idEquipoA, Estado = EstadoPartido.Programado });
        }

        return partidos;
    }

    /// <summary>
    /// Partido que decide el cruce: el único, o el de VUELTA (numero_partido = 2) si es ida y vuelta.
    /// Es el partido donde se registran los penales.
    /// </summary>
    public static Partido? PartidoDecisivo(IEnumerable<Partido> partidosDelCruce) =>
        partidosDelCruce.OrderByDescending(p => p.NumeroPartido).FirstOrDefault();

    // ------------------------------------------------------------------
    // Quién gana
    // ------------------------------------------------------------------

    /// <summary>
    /// Goles acumulados de cada equipo del cruce, sumando todos sus partidos (la vuelta tiene la localía
    /// invertida, por eso se compara contra el equipo A y no contra "local"). Un marcador sin definir cuenta 0.
    /// </summary>
    public static (int GolesA, int GolesB) Global(Cruce cruce, IEnumerable<Partido> partidos)
    {
        int golesA = 0;
        int golesB = 0;

        foreach (var partido in partidos)
        {
            int local = partido.GolesLocal ?? 0;
            int visitante = partido.GolesVisitante ?? 0;

            if (partido.IdLocal == cruce.IdEquipoA)
            {
                golesA += local;
                golesB += visitante;
            }
            else
            {
                golesA += visitante;
                golesB += local;
            }
        }

        return (golesA, golesB);
    }

    /// <summary>
    /// Evalúa el cruce con los partidos que trae cargados (cruce.Partidos):
    ///   - Si falta algún partido por finalizar, el cruce NO está completo y no hay ganador.
    ///   - Si el global no está empatado, gana el que tiene más goles.
    ///   - Si está empatado, gana el de más penales del partido decisivo; sin penales válidos
    ///     (faltan, o son iguales) queda como Empate pendiente.
    /// </summary>
    public static DecisionCruce Decidir(Cruce cruce, bool dosPartidos)
    {
        var partidos = cruce.Partidos.ToList();
        var (golesA, golesB) = Global(cruce, partidos);

        int esperados = dosPartidos ? 2 : 1;

        bool completo = cruce.IdEquipoA.HasValue
            && cruce.IdEquipoB.HasValue
            && partidos.Count == esperados
            && partidos.All(p => p.Estado == EstadoPartido.Finalizado);

        if (!completo)
        {
            return new DecisionCruce(false, false, null, golesA, golesB);
        }

        if (golesA != golesB)
        {
            return new DecisionCruce(true, false, golesA > golesB ? cruce.IdEquipoA : cruce.IdEquipoB, golesA, golesB);
        }

        var decisivo = PartidoDecisivo(partidos)!;

        if (decisivo.PenalesLocal is int penalesLocal
            && decisivo.PenalesVisitante is int penalesVisitante
            && penalesLocal != penalesVisitante
            && decisivo.IdVisitante.HasValue)
        {
            int idGanador = penalesLocal > penalesVisitante ? decisivo.IdLocal : decisivo.IdVisitante.Value;
            return new DecisionCruce(true, false, idGanador, golesA, golesB);
        }

        return new DecisionCruce(true, true, null, golesA, golesB);
    }

    // ------------------------------------------------------------------
    // Penales
    // ------------------------------------------------------------------

    /// <summary>
    /// Valida los valores del formulario de penales. Devuelve el mensaje de error, o null si están bien.
    /// Los dos vacíos significan "quitar los penales" y es válido.
    /// </summary>
    public static string? ValidarPenales(int? penalesLocal, int? penalesVisitante)
    {
        if (penalesLocal is null && penalesVisitante is null)
        {
            return null;
        }

        if (penalesLocal is null || penalesVisitante is null)
        {
            return "Indica los penales de los dos equipos.";
        }

        if (penalesLocal < 0 || penalesLocal > MaxPenales || penalesVisitante < 0 || penalesVisitante > MaxPenales)
        {
            return $"Los penales de cada equipo deben estar entre 0 y {MaxPenales}.";
        }

        if (penalesLocal == penalesVisitante)
        {
            return "No puede haber empate en penales: uno de los dos equipos tiene que ganar la tanda.";
        }

        return null;
    }

    /// <summary>
    /// Motivo por el que NO se pueden registrar penales ahora en este partido (null = sí se puede).
    /// El cruce debe venir con sus partidos cargados. Quién puede hacerlo (organizador o árbitro del partido)
    /// se comprueba aparte; aquí solo cambia el estado permitido: el árbitro solo con el partido En vivo,
    /// el organizador también con el partido Finalizado (para corregir).
    /// </summary>
    public static string? MotivoBloqueoPenales(
        Partido partido, Cruce cruce, bool dosPartidos, EstadoTorneo estadoTorneo, bool esOrganizador)
    {
        if (estadoTorneo == EstadoTorneo.Finalizado)
        {
            return "El torneo ya finalizó; no se pueden modificar los penales.";
        }

        bool esDecisivo = !dosPartidos || partido.NumeroPartido == 2;

        if (!esDecisivo)
        {
            return "Los penales se registran en el partido de vuelta, que es el que decide el cruce.";
        }

        bool estadoPermitido = partido.Estado == EstadoPartido.EnVivo
            || (esOrganizador && partido.Estado == EstadoPartido.Finalizado);

        if (!estadoPermitido)
        {
            return esOrganizador
                ? "Los penales solo se registran con el partido En vivo (o se corrigen si está Finalizado)."
                : "Los penales solo se pueden registrar con el partido En vivo.";
        }

        if (cruce.IdGanador.HasValue)
        {
            return "El cruce ya tiene ganador. Reabre el partido para poder corregir los penales.";
        }

        // En ida y vuelta, la ida tiene que haberse jugado para saber si el global está empatado.
        if (dosPartidos && cruce.Partidos.Any(p => p.NumeroPartido == 1 && p.GolesLocal is null))
        {
            return "Primero debe jugarse el partido de ida.";
        }

        var (golesA, golesB) = Global(cruce, cruce.Partidos);

        if (golesA != golesB)
        {
            return dosPartidos
                ? "Los penales solo se piden si el marcador global está empatado."
                : "Los penales solo se piden si el partido está empatado.";
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Reapertura de la final
    // ------------------------------------------------------------------

    /// <summary>
    /// Con el torneo Finalizado nada se modifica, salvo UNA excepción: el organizador puede reabrir la final
    /// de una copa para corregirla. Al hacerlo, el torneo vuelve a estar En curso.
    /// Requiere el partido con Ronda -> Fase -> Torneo y Cruce cargados.
    /// </summary>
    public static bool PuedeReabrirFinal(Partido partido)
    {
        var torneo = ReglasPartido.TorneoDe(partido);

        return torneo.Estado == EstadoTorneo.Finalizado
            && (torneo.Formato == FormatoTorneo.Copa || torneo.Formato == FormatoTorneo.GruposCopa)
            && partido.Estado == EstadoPartido.Finalizado
            && partido.Cruce is not null
            && partido.Cruce.IdCruceSiguiente is null;
    }

    // ------------------------------------------------------------------
    // Textos para mostrar
    // ------------------------------------------------------------------

    /// <summary>Penales para mostrar junto al marcador: "(4 - 3 pen.)", o vacío si el partido no tiene penales.</summary>
    public static string TextoPenales(Partido partido) =>
        partido.PenalesLocal.HasValue && partido.PenalesVisitante.HasValue
            ? $"({partido.PenalesLocal} - {partido.PenalesVisitante} pen.)"
            : string.Empty;
}
