using FairPlay.Models;
using FairPlay.ViewModels;

namespace FairPlay.Services;

/// <summary>
/// Lógica de negocio de los jugadores de un equipo. Solo el responsable del equipo o el organizador
/// del torneo pueden agregar, editar o eliminar jugadores.
///
/// Un jugador siempre se alcanza a través del código de SU torneo y el id de SU equipo.
/// </summary>
public interface IServicioJugadores
{
    /// <summary>Para mostrar el formulario de agregar: comprueba permiso y que el torneo permita cambios.</summary>
    Task<ResultadoServicio<Equipo>> ObtenerEquipoParaAgregarAsync(int idUsuario, string? codigoTorneo, int idEquipo);

    /// <summary>Agrega un jugador al equipo (dorsal único; si es capitán, el anterior deja de serlo).</summary>
    Task<ResultadoServicio<Jugador>> AgregarAsync(int idUsuario, string? codigoTorneo, int idEquipo, JugadorFormViewModel datos);

    /// <summary>Jugador para EDITAR o ELIMINAR: comprueba que el usuario sea el responsable o el organizador.</summary>
    Task<ResultadoServicio<Jugador>> ObtenerParaGestionAsync(int idUsuario, string? codigoTorneo, int idEquipo, int idJugador);

    Task<ResultadoServicio<Jugador>> EditarAsync(int idUsuario, string? codigoTorneo, int idEquipo, int idJugador, JugadorFormViewModel datos);

    /// <summary>Elimina un jugador. No permite eliminar al último: un equipo debe tener al menos uno.</summary>
    Task<ResultadoServicio<Jugador>> EliminarAsync(int idUsuario, string? codigoTorneo, int idEquipo, int idJugador);
}
