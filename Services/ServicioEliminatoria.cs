using FairPlay.Data;
using FairPlay.Models;
using FairPlay.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

public class ServicioEliminatoria : IServicioEliminatoria
{
    private readonly FairPlayContext _contexto;
    private readonly ILogger<ServicioEliminatoria> _logger;

    // ASP.NET Core entrega los dos objetos solos (inyección de dependencias).
    // OJO: el FairPlayContext es "Scoped": dentro de una misma petición, ServicioPartidos, ServicioEventos y
    // este servicio reciben EL MISMO contexto. Por eso lo que este servicio modifica sin guardar lo guarda
    // después el SaveChanges de quien lo llamó.
    public ServicioEliminatoria(FairPlayContext contexto, ILogger<ServicioEliminatoria> logger)
    {
        _contexto = contexto;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // CONSULTAS (públicas)
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<LlaveTorneo>> ObtenerLlaveAsync(string? codigoTorneo)
    {
        var codigo = ReglasEquipo.NormalizarCodigo(codigoTorneo);
        var torneo = await _contexto.Torneos.AsNoTracking().FirstOrDefaultAsync(t => t.Codigo == codigo);

        if (torneo is null)
        {
            return ResultadoServicio<LlaveTorneo>.NoEncontrado();
        }

        // Solo las rondas de las fases de eliminatoria (en Grupos + Copa será la fase 2).
        // AsNoTracking: solo se lee. Cada cruce trae sus equipos, su ganador y sus partidos.
        var rondas = await _contexto.Rondas
            .AsNoTracking()
            .Include(r => r.Cruces).ThenInclude(c => c.EquipoA)
            .Include(r => r.Cruces).ThenInclude(c => c.EquipoB)
            .Include(r => r.Cruces).ThenInclude(c => c.Ganador)
            .Include(r => r.Cruces).ThenInclude(c => c.Partidos)
            .Where(r => r.Fase!.IdTorneo == torneo.IdTorneo && r.Fase!.Tipo == TipoFase.Eliminatoria)
            .OrderBy(r => r.Numero)
            .ToListAsync();

        // El campeón es el ganador del único cruce sin "cruce siguiente" (la final), si ya se decidió.
        var campeon = rondas
            .SelectMany(r => r.Cruces)
            .Where(c => c.IdCruceSiguiente == null && c.IdGanador != null)
            .Select(c => c.Ganador)
            .FirstOrDefault();

        return ResultadoServicio<LlaveTorneo>.Correcto(new LlaveTorneo(torneo, rondas, campeon));
    }

    public async Task<InfoEliminatoria?> ObtenerInfoAsync(int? idUsuario, Partido partido)
    {
        if (partido.IdCruce is null)
        {
            return null;
        }

        var cruce = await _contexto.Cruces
            .AsNoTracking()
            .Include(c => c.Partidos)
            .Include(c => c.EquipoA)
            .Include(c => c.EquipoB)
            .Include(c => c.Ganador)
            .FirstOrDefaultAsync(c => c.IdCruce == partido.IdCruce.Value);

        if (cruce is null)
        {
            return null;
        }

        var torneo = ReglasPartido.TorneoDe(partido);
        bool dosPartidos = ReglasEliminatoria.EsIdaYVuelta(partido.Ronda!.Fase!.IdaYVuelta, cruce);
        bool esDecisivo = !dosPartidos || partido.NumeroPartido == 2;

        bool esOrganizador = ReglasPartido.EsOrganizador(partido, idUsuario);
        bool esArbitro = ReglasPartido.EsArbitro(partido, idUsuario);

        var (golesA, golesB) = ReglasEliminatoria.Global(cruce, cruce.Partidos);
        bool todosTienenMarcador = cruce.Partidos.All(p => p.GolesLocal.HasValue);

        // El global solo tiene sentido en una ida y vuelta, y cuando ya se jugó (al menos empezó) la ida.
        string? textoGlobal = null;

        if (dosPartidos && cruce.Partidos.Any(p => p.GolesLocal.HasValue))
        {
            textoGlobal = $"Global: {cruce.EquipoA?.Nombre} {golesA} - {golesB} {cruce.EquipoB?.Nombre}";
        }

        bool puedePenales = (esOrganizador || esArbitro)
            && ReglasEliminatoria.MotivoBloqueoPenales(partido, cruce, dosPartidos, torneo.Estado, esOrganizador) is null;

        return new InfoEliminatoria(
            EsIdaYVuelta: dosPartidos,
            EsFinal: cruce.IdCruceSiguiente is null,
            EsPartidoDecisivo: esDecisivo,
            CruceDecidido: cruce.IdGanador.HasValue,
            NombreGanador: cruce.Ganador?.Nombre,
            TextoGlobal: textoGlobal,
            EmpatadoSinDefinir: todosTienenMarcador && golesA == golesB,
            PuedeRegistrarPenales: puedePenales);
    }

    // ------------------------------------------------------------------
    // PENALES
    // ------------------------------------------------------------------

    public async Task<ResultadoServicio<Partido>> RegistrarPenalesAsync(
        int idUsuario, string? codigoTorneo, int idPartido, int? penalesLocal, int? penalesVisitante)
    {
        // Sin AsNoTracking: EF debe seguir el partido para guardar los penales.
        var partido = await _contexto.PartidoDeTorneo(codigoTorneo, idPartido).FirstOrDefaultAsync();

        if (partido is null)
        {
            return ResultadoServicio<Partido>.NoEncontrado();
        }

        var torneo = ReglasPartido.TorneoDe(partido);
        bool esOrganizador = torneo.IdOrganizador == idUsuario;
        bool esArbitro = partido.IdArbitro == idUsuario;

        // 1) QUIÉN: solo el organizador o el árbitro de ESTE partido (el resto recibe 403).
        if (!esOrganizador && !esArbitro)
        {
            return ResultadoServicio<Partido>.SinPermiso();
        }

        // 2) CUÁNDO: el partido debe ser de un cruce y estar en el momento correcto.
        if (partido.IdCruce is null)
        {
            return ResultadoServicio<Partido>.Invalido(string.Empty, "Este partido no es de una eliminatoria: no tiene penales.");
        }

        var cruce = await CargarCruceAsync(partido.IdCruce.Value);

        if (cruce is null)
        {
            return ResultadoServicio<Partido>.NoEncontrado();
        }

        bool dosPartidos = ReglasEliminatoria.EsIdaYVuelta(partido.Ronda!.Fase!.IdaYVuelta, cruce);

        var bloqueo = ReglasEliminatoria.MotivoBloqueoPenales(partido, cruce, dosPartidos, torneo.Estado, esOrganizador);

        if (bloqueo is not null)
        {
            return ResultadoServicio<Partido>.Invalido(string.Empty, bloqueo);
        }

        // 3) DATOS DEL FORMULARIO
        var errorDatos = ReglasEliminatoria.ValidarPenales(penalesLocal, penalesVisitante);

        if (errorDatos is not null)
        {
            return ResultadoServicio<Partido>.Invalido("penalesLocal", errorDatos);
        }

        // 4) EFECTOS: los penales NO tocan el marcador ni los eventos.
        partido.PenalesLocal = penalesLocal;
        partido.PenalesVisitante = penalesVisitante;

        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudieron guardar los penales del partido {IdPartido}.", idPartido);
            return ResultadoServicio<Partido>.Invalido(string.Empty, "No se pudieron guardar los penales. Inténtalo de nuevo.");
        }

        return ResultadoServicio<Partido>.Correcto(partido);
    }

    // ------------------------------------------------------------------
    // AVANCE DE GANADORES (no guarda: lo guarda quien llama)
    // ------------------------------------------------------------------

    public async Task<string?> AplicarCambioDeEstadoAsync(Partido partido, EstadoPartido estadoAnterior, EstadoPartido estadoNuevo)
    {
        // Los partidos de liga o suizo no tienen cruce: nada que hacer.
        if (partido.IdCruce is null)
        {
            return null;
        }

        bool finaliza = estadoNuevo == EstadoPartido.Finalizado;
        bool reabre = estadoAnterior == EstadoPartido.Finalizado && estadoNuevo == EstadoPartido.EnVivo;

        // Solo Finalizar y Reabrir afectan a la llave (Iniciar o Suspender no cambian quién gana).
        if (!finaliza && !reabre)
        {
            return null;
        }

        var cruce = await CargarCruceAsync(partido.IdCruce.Value);

        if (cruce is null)
        {
            return null;
        }

        // Ronda -> Fase -> Torneo vienen cargados (ReglasPartido.PartidoDeTorneo) y el contexto los sigue:
        // lo que se cambie en ellos se guarda junto con el partido.
        var fase = partido.Ronda!.Fase!;
        var torneo = fase.Torneo!;

        return reabre
            ? await RetirarAvanceAsync(cruce, fase, torneo)
            : await AvanzarSiCorrespondeAsync(partido, cruce, fase, torneo);
    }

    /// <summary>Se finalizó un partido de un cruce: decide si el cruce quedó resuelto y, si sí, avanza al ganador.</summary>
    private async Task<string?> AvanzarSiCorrespondeAsync(Partido partido, Cruce cruce, Fase fase, Torneo torneo)
    {
        bool dosPartidos = ReglasEliminatoria.EsIdaYVuelta(fase.IdaYVuelta, cruce);

        // La vuelta cierra el cruce: para sumar el global, la ida ya tiene que haber terminado.
        if (dosPartidos
            && partido.NumeroPartido == 2
            && cruce.Partidos.Any(p => p.NumeroPartido == 1 && p.Estado != EstadoPartido.Finalizado))
        {
            return "Primero debe finalizar el partido de ida: la vuelta cierra el cruce con el marcador global.";
        }

        // cruce.Partidos incluye este mismo partido ya marcado como Finalizado (el contexto es el mismo).
        var decision = ReglasEliminatoria.Decidir(cruce, dosPartidos);

        if (!decision.Completo)
        {
            return null; // falta jugar el otro partido del cruce: todavía no hay nada que avanzar
        }

        if (decision.Empate)
        {
            // REGLA: no se puede finalizar el partido que cierra el cruce mientras siga empatado sin penales.
            string a = cruce.EquipoA?.Nombre ?? "Equipo A";
            string b = cruce.EquipoB?.Nombre ?? "Equipo B";

            return dosPartidos
                ? $"El cruce está empatado en el global ({a} {decision.GolesA} - {decision.GolesB} {b}). Registra los penales en el partido de vuelta antes de finalizar."
                : $"El partido terminó empatado ({a} {decision.GolesA} - {decision.GolesB} {b}). Registra los penales antes de finalizar.";
        }

        // Si el cruce NO quedó empatado, los penales que hubiera guardados ya no corresponden.
        var decisivo = ReglasEliminatoria.PartidoDecisivo(cruce.Partidos)!;
        bool huboEmpate = decision.GolesA == decision.GolesB;

        if (!huboEmpate && (decisivo.PenalesLocal.HasValue || decisivo.PenalesVisitante.HasValue))
        {
            decisivo.PenalesLocal = null;
            decisivo.PenalesVisitante = null;
        }

        cruce.IdGanador = decision.IdGanador;

        return await AvanzarGanadorAsync(cruce, fase, torneo);
    }

    /// <summary>
    /// Coloca al ganador en el cruce siguiente (creando sus partidos cuando ya están los dos equipos) o, si el
    /// cruce era la final, cierra la fase y el torneo.
    /// </summary>
    private async Task<string?> AvanzarGanadorAsync(Cruce cruce, Fase fase, Torneo torneo)
    {
        if (cruce.IdCruceSiguiente is null)
        {
            // Era la final: hay campeón. La fase se cierra y el torneo finaliza (solo con una final decidida).
            fase.Estado = EstadoFase.Cerrada;
            torneo.Estado = EstadoTorneo.Finalizado;
            return null;
        }

        var siguiente = await CargarCruceAsync(cruce.IdCruceSiguiente.Value);

        if (siguiente is null)
        {
            return null;
        }

        // Posición impar -> lugar A del cruce siguiente; posición par -> lugar B.
        if (cruce.Posicion % 2 == 1)
        {
            siguiente.IdEquipoA = cruce.IdGanador;
        }
        else
        {
            siguiente.IdEquipoB = cruce.IdGanador;
        }

        // Los partidos de un cruce solo existen cuando se conocen sus dos equipos (partido.id_local es obligatorio).
        if (siguiente.IdEquipoA.HasValue && siguiente.IdEquipoB.HasValue && siguiente.Partidos.Count == 0)
        {
            bool dosPartidos = ReglasEliminatoria.EsIdaYVuelta(fase.IdaYVuelta, siguiente);

            foreach (var nuevo in ReglasEliminatoria.PartidosDelCruce(siguiente.IdEquipoA.Value, siguiente.IdEquipoB.Value, dosPartidos))
            {
                nuevo.IdRonda = siguiente.IdRonda;
                nuevo.IdCruce = siguiente.IdCruce;
                _contexto.Partidos.Add(nuevo);
            }
        }

        return null;
    }

    /// <summary>
    /// Se reabrió un partido Finalizado: si su cruce ya tenía ganador, se retira el avance. Se rechaza si el
    /// siguiente cruce ya tiene partidos iniciados o jugados (no se puede deshacer lo que ya se jugó).
    /// </summary>
    private async Task<string?> RetirarAvanceAsync(Cruce cruce, Fase fase, Torneo torneo)
    {
        if (cruce.IdGanador is null)
        {
            return null; // el cruce aún no estaba decidido (por ejemplo, se reabre la ida antes de jugar la vuelta)
        }

        if (cruce.IdCruceSiguiente is null)
        {
            // Se reabre la final: ya no hay campeón y el torneo vuelve a estar en curso.
            cruce.IdGanador = null;
            fase.Estado = EstadoFase.EnCurso;
            torneo.Estado = EstadoTorneo.EnCurso;
            return null;
        }

        var siguiente = await CargarCruceAsync(cruce.IdCruceSiguiente.Value);

        if (siguiente is null)
        {
            cruce.IdGanador = null;
            return null;
        }

        // ¿Ya se tocó algo en el cruce siguiente? Basta con que algún partido haya empezado, tenga marcador,
        // historial de estados o eventos.
        bool hayActividad = await _contexto.Partidos.AnyAsync(p =>
            p.IdCruce == siguiente.IdCruce
            && (p.Estado != EstadoPartido.Programado
                || p.GolesLocal != null
                || p.CambiosEstado.Any()
                || p.Eventos.Any()));

        if (hayActividad)
        {
            return "No se puede reabrir este partido: el ganador ya avanzó y el siguiente cruce tiene partidos iniciados o jugados.";
        }

        // Se deshace el avance: los partidos del siguiente cruce (todavía sin jugar) se borran y su lugar queda libre.
        _contexto.Partidos.RemoveRange(siguiente.Partidos.ToList());

        if (cruce.Posicion % 2 == 1)
        {
            siguiente.IdEquipoA = null;
        }
        else
        {
            siguiente.IdEquipoB = null;
        }

        cruce.IdGanador = null;

        return null;
    }

    public async Task LimpiarPenalesObsoletosAsync(Partido partido)
    {
        if (partido.IdCruce is null)
        {
            return;
        }

        var cruce = await CargarCruceAsync(partido.IdCruce.Value);

        if (cruce is null)
        {
            return;
        }

        var decisivo = ReglasEliminatoria.PartidoDecisivo(cruce.Partidos);

        if (decisivo is null || !decisivo.PenalesLocal.HasValue)
        {
            return;
        }

        // cruce.Partidos incluye el partido con el marcador que acaba de cambiar (mismo contexto).
        var (golesA, golesB) = ReglasEliminatoria.Global(cruce, cruce.Partidos);

        if (golesA != golesB)
        {
            decisivo.PenalesLocal = null;
            decisivo.PenalesVisitante = null;
        }
    }

    public async Task<string?> MotivoBloqueoEventosAsync(Partido partido)
    {
        if (partido.IdCruce is null)
        {
            return null;
        }

        bool decidido = await _contexto.Cruces
            .AsNoTracking()
            .AnyAsync(c => c.IdCruce == partido.IdCruce.Value && c.IdGanador != null);

        return decidido
            ? "El ganador de este cruce ya avanzó: cambiar un gol podría cambiar quién avanza. Reabre el partido primero para corregirlo."
            : null;
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados
    // ------------------------------------------------------------------

    /// <summary>
    /// Carga un cruce CON seguimiento (tracking), junto con sus partidos y sus equipos. Con seguimiento, EF
    /// reutiliza las instancias que el contexto ya tiene (por ejemplo el partido que se acaba de modificar),
    /// así cruce.Partidos refleja los cambios aún no guardados.
    /// </summary>
    private Task<Cruce?> CargarCruceAsync(int idCruce) =>
        _contexto.Cruces
            .Include(c => c.Partidos)
            .Include(c => c.EquipoA)
            .Include(c => c.EquipoB)
            .FirstOrDefaultAsync(c => c.IdCruce == idCruce);
}
