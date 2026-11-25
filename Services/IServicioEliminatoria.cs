using FairPlay.Models;
using FairPlay.Models.Enums;

namespace FairPlay.Services;

/// <summary>
/// La llave de un torneo con eliminatoria, lista para mostrar: las rondas (con sus cruces, equipos y partidos
/// cargados) y el campeón cuando la final ya se decidió.
/// </summary>
public record LlaveTorneo(Torneo Torneo, IReadOnlyList<Ronda> Rondas, Equipo? Campeon);

/// <summary>
/// Información de eliminatoria de UN partido, para su pantalla de detalle (null si no es de una eliminatoria).
/// </summary>
/// <param name="EsIdaYVuelta">El cruce se juega a ida y vuelta.</param>
/// <param name="EsFinal">Es el cruce de la final (siempre a partido único).</param>
/// <param name="EsPartidoDecisivo">Es el partido que decide el cruce: el único, o la vuelta.</param>
/// <param name="CruceDecidido">El cruce ya tiene ganador (ya avanzó al cruce siguiente o ganó el torneo).</param>
/// <param name="NombreGanador">Nombre del ganador del cruce (null si todavía no hay).</param>
/// <param name="TextoGlobal">Marcador global de una ida y vuelta, por ejemplo "Global: Águilas 3 - 2 Leones".</param>
/// <param name="EmpatadoSinDefinir">El marcador (o el global) está empatado: se define por penales.</param>
/// <param name="PuedeRegistrarPenales">ESTE usuario puede registrar los penales de este partido ahora.</param>
public record InfoEliminatoria(
    bool EsIdaYVuelta,
    bool EsFinal,
    bool EsPartidoDecisivo,
    bool CruceDecidido,
    string? NombreGanador,
    string? TextoGlobal,
    bool EmpatadoSinDefinir,
    bool PuedeRegistrarPenales);

/// <summary>
/// Lógica de negocio de la eliminatoria (Paso 6, entrega C: Copa): la llave, los penales y el avance de los
/// ganadores al siguiente cruce.
///
/// Hay dos tipos de operaciones:
///   - Las que SÍ guardan (ObtenerLlaveAsync lee; RegistrarPenalesAsync guarda): se llaman desde el controlador.
///   - Las que NO guardan (AplicarCambioDeEstadoAsync, LimpiarPenalesObsoletosAsync, MotivoBloqueoEventosAsync):
///     las llaman ServicioPartidos y ServicioEventos DENTRO de su propia operación. Modifican las entidades que
///     el contexto ya sigue, y quien las llamó hace el único SaveChanges, así el cambio del partido y el avance
///     del ganador se guardan JUNTOS en una sola transacción (o se guarda todo o no se guarda nada).
/// </summary>
public interface IServicioEliminatoria
{
    /// <summary>
    /// La llave del torneo. Es pública: conocer el código del torneo es la "llave" para verla.
    /// Si el torneo no tiene eliminatoria todavía, la lista de rondas viene vacía.
    /// </summary>
    Task<ResultadoServicio<LlaveTorneo>> ObtenerLlaveAsync(string? codigoTorneo);

    /// <summary>
    /// Datos de eliminatoria de un partido para su detalle (null si el partido no es de un cruce).
    /// idUsuario null = visitante sin sesión. El partido debe venir con Ronda -> Fase -> Torneo cargados.
    /// </summary>
    Task<InfoEliminatoria?> ObtenerInfoAsync(int? idUsuario, Partido partido);

    /// <summary>
    /// Registra (o quita, si los dos valores vienen vacíos) los penales de un partido de eliminatoria.
    /// Permisos: organizador o árbitro del partido. Ver ReglasEliminatoria.MotivoBloqueoPenales.
    /// </summary>
    Task<ResultadoServicio<Partido>> RegistrarPenalesAsync(
        int idUsuario, string? codigoTorneo, int idPartido, int? penalesLocal, int? penalesVisitante);

    /// <summary>
    /// NO GUARDA. Aplica a la llave el cambio de estado de un partido que YA se modificó en memoria:
    ///   - Al FINALIZAR: si el cruce queda decidido, el ganador avanza al cruce siguiente (y se crean sus
    ///     partidos cuando ya están los dos equipos); si es la final, el torneo pasa a Finalizado.
    ///     Si queda empatado sin penales, o la vuelta se finaliza sin haber finalizado la ida, rechaza.
    ///   - Al REABRIR un partido Finalizado: retira el avance del ganador, salvo que el siguiente cruce ya
    ///     tenga partidos jugados (entonces rechaza). Al reabrir la final, el torneo vuelve a En curso.
    /// Devuelve el mensaje de error, o null si todo está bien. Si devuelve un error, quien llamó NO debe guardar.
    /// </summary>
    Task<string?> AplicarCambioDeEstadoAsync(Partido partido, EstadoPartido estadoAnterior, EstadoPartido estadoNuevo);

    /// <summary>
    /// NO GUARDA. Si el marcador del cruce dejó de estar empatado (por un gol agregado o eliminado), borra los
    /// penales que hubieran quedado registrados: ya no hacen falta.
    /// </summary>
    Task LimpiarPenalesObsoletosAsync(Partido partido);

    /// <summary>
    /// Motivo por el que NO se pueden registrar ni eliminar eventos de este partido (null = sí se puede):
    /// cuando su cruce ya tiene ganador, cambiar un gol podría cambiar quién avanzó. Hay que reabrir primero.
    /// </summary>
    Task<string?> MotivoBloqueoEventosAsync(Partido partido);
}
