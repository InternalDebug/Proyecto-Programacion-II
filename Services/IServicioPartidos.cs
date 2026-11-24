using FairPlay.Models;
using FairPlay.Models.Enums;

namespace FairPlay.Services;

/// <summary>
/// Lógica de negocio de los partidos (Paso 4): ver partidos, designar árbitro, fijar la fecha y
/// cambiar el estado con su historial. El controlador solo traduce el resultado a HTTP.
///
/// Igual que en equipos y jugadores, todo partido se alcanza a través del CÓDIGO del torneo,
/// y cada operación que modifica datos vuelve a comprobar el permiso (el POST puede enviarse a mano).
///
/// Roles que intervienen (contextuales, ninguno vive en la cookie):
///   Organizador -> torneo.id_organizador.   Árbitro -> partido.id_arbitro.   Espectador -> cualquiera.
/// </summary>
public interface IServicioPartidos
{
    /// <summary>
    /// Partidos de un torneo, ordenados por fase, ronda y partido (con equipos, árbitro y ronda cargados).
    /// Es pública: no comprueba permisos, porque conocer el código del torneo es la "llave" para verlo.
    /// </summary>
    Task<List<Partido>> ListarDeTorneoAsync(int idTorneo);

    /// <summary>
    /// Un partido para mostrar su detalle. Cualquiera con el código lo ve (idUsuario null = visitante).
    /// El HISTORIAL de cambios de estado (partido.CambiosEstado) solo se carga si el usuario es el
    /// organizador del torneo; para el resto queda vacío.
    /// </summary>
    Task<ResultadoServicio<Partido>> ObtenerParaVerAsync(int? idUsuario, string? codigoTorneo, int idPartido);

    /// <summary>Partidos en los que el usuario es árbitro (primero los que se juegan ahora).</summary>
    Task<List<Partido>> ListarArbitradosAsync(int idUsuario);

    /// <summary>Datos del enlace "Mis partidos" del menú.</summary>
    Task<ResumenArbitraje> ObtenerResumenArbitrajeAsync(int idUsuario);

    /// <summary>
    /// Designa al árbitro del partido por su NOMBRE DE USUARIO (null o vacío = quitar al árbitro).
    /// Solo el organizador. Un responsable de equipo de ESTE torneo no puede ser árbitro en él
    /// (en otros torneos sí); el organizador está exento porque en un torneo cerrado él inscribe los equipos.
    /// </summary>
    Task<ResultadoServicio<Partido>> AsignarArbitroAsync(int idUsuario, string? codigoTorneo, int idPartido, string? nombreArbitro);

    /// <summary>Fija o cambia la fecha y hora del partido (null = quitarla). Solo el organizador.</summary>
    Task<ResultadoServicio<Partido>> ProgramarFechaAsync(int idUsuario, string? codigoTorneo, int idPartido, DateTime? fechaHora);

    /// <summary>
    /// Cambia el estado del partido y deja constancia en el historial (cambio_estado_partido), todo
    /// en la misma transacción. Permisos: ver ReglasPartido.PuedeCambiar.
    /// Al pasar a EnVivo, si el partido no tiene marcador empieza en 0 - 0; si ya lo tenía (por ejemplo
    /// venía de Suspendido) se CONSERVA.
    /// </summary>
    Task<ResultadoServicio<Partido>> CambiarEstadoAsync(int idUsuario, string? codigoTorneo, int idPartido, EstadoPartido nuevoEstado);
}
