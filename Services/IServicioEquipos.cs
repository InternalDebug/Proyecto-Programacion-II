using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.ViewModels;

namespace FairPlay.Services;

/// <summary>
/// Lógica de negocio de los equipos: inscripción según el modo del torneo, permisos,
/// aprobación del organizador y escudos.
///
/// Igual que con los torneos, el torneo se identifica siempre por su CÓDIGO (nunca por su id
/// numérico) y un equipo solo se alcanza a través del código de SU torneo.
/// </summary>
public interface IServicioEquipos
{
    /// <summary>
    /// Equipos del torneo que ESTE usuario puede ver: los Aprobados para cualquiera; además,
    /// el organizador ve todos y cada responsable ve los suyos (aunque estén Pendientes o Rechazados).
    /// </summary>
    Task<List<Equipo>> ListarVisiblesAsync(int idTorneo, int? idUsuario, bool esOrganizador);

    /// <summary>
    /// ¿Puede este usuario inscribir un equipo en el torneo AHORA? Aplica el modo de inscripción,
    /// el estado del torneo y el máximo de equipos.
    /// Devuelve Correcto (con el torneo), NoEncontrado, o Invalido con el motivo en Mensaje.
    /// </summary>
    Task<ResultadoServicio<Torneo>> ObtenerTorneoParaInscribirAsync(int idUsuario, string? codigoTorneo);

    /// <summary>
    /// Inscribe un equipo junto con su primer jugador (se guardan juntos). Según el modo del torneo
    /// queda Aprobado (organizador o modo Automática) o Pendiente (modo ConAprobación).
    /// </summary>
    Task<ResultadoServicio<Equipo>> InscribirAsync(int idUsuario, string? codigoTorneo, InscribirEquipoViewModel datos);

    /// <summary>Equipo con sus jugadores para mostrarlo. Los Pendientes/Rechazados solo los ve el organizador o el responsable.</summary>
    Task<ResultadoServicio<Equipo>> ObtenerParaVerAsync(int? idUsuario, string? codigoTorneo, int idEquipo);

    /// <summary>Equipo para EDITAR o ELIMINAR: comprueba que el usuario sea el responsable o el organizador.</summary>
    Task<ResultadoServicio<Equipo>> ObtenerParaGestionAsync(int idUsuario, string? codigoTorneo, int idEquipo);

    /// <summary>Modifica nombre, ciudad y escudo. No cambia el estado de inscripción.</summary>
    Task<ResultadoServicio<Equipo>> EditarAsync(int idUsuario, string? codigoTorneo, int idEquipo, EquipoFormViewModel datos);

    /// <summary>Elimina el equipo (y en cascada sus jugadores) y su archivo de escudo.</summary>
    Task<ResultadoServicio<Equipo>> EliminarAsync(int idUsuario, string? codigoTorneo, int idEquipo);

    /// <summary>Aprobar o Rechazar una inscripción. Solo el organizador del torneo.</summary>
    Task<ResultadoServicio<Equipo>> DecidirInscripcionAsync(int idUsuario, string? codigoTorneo, int idEquipo, EstadoInscripcion decision);

    /// <summary>
    /// Pantalla "Solicitudes de inscripción": equipos Pendientes y Rechazados de los torneos que
    /// organiza el usuario (los Rechazados se listan para poder eliminarlos y liberar su nombre).
    /// </summary>
    Task<List<Equipo>> ListarSolicitudesAsync(int idOrganizador);

    /// <summary>Datos para el enlace "Solicitudes de inscripción" de la barra de navegación.</summary>
    Task<ResumenSolicitudes> ObtenerResumenSolicitudesAsync(int idUsuario);
}
