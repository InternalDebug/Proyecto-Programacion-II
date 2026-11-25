using FairPlay.ViewModels;

namespace FairPlay.Services;

/// <summary>
/// Lógica de negocio de los eventos de partido (Paso 5): goles, autogoles y tarjetas.
///
/// Reglas principales:
///   - El MARCADOR ES AUTOMÁTICO: cada Gol (o Autogol) suma al marcador y cada eliminación lo resta;
///     el evento y el marcador se guardan juntos, en una sola transacción.
///   - Árbitro: solo con el partido EnVivo. Organizador: EnVivo y Finalizado (para corregir).
///   - La segunda amarilla de un jugador en el partido agrega sola una roja.
///   - No se editan eventos: se elimina el equivocado y se registra de nuevo.
///
/// Como en el resto de servicios, todo partido se alcanza por el CÓDIGO del torneo y cada operación
/// vuelve a comprobar los permisos (el POST puede enviarse a mano).
/// </summary>
public interface IServicioEventos
{
    /// <summary>
    /// Registra un evento. Permisos: organizador o árbitro del partido (ver ReglasEvento.PuedeModificar).
    /// Un Gol puede ir sin jugador (entonces hay que indicar el equipo); el resto exige jugador.
    /// </summary>
    Task<ResultadoServicio<ResultadoEvento>> RegistrarAsync(
        int idUsuario, string? codigoTorneo, int idPartido, EventoFormViewModel datos);

    /// <summary>
    /// Elimina un evento del partido y le resta su aporte al marcador.
    /// Si es la segunda amarilla de una expulsión, la roja automática también se elimina.
    /// </summary>
    Task<ResultadoServicio<ResultadoEvento>> EliminarAsync(
        int idUsuario, string? codigoTorneo, int idPartido, int idEvento);

    /// <summary>Jugadores de los dos equipos del partido, para el formulario de la pantalla de detalle.</summary>
    Task<PlantillasPartido> ListarPlantillasAsync(int idLocal, int? idVisitante);
}
