using FairPlay.Models;

namespace FairPlay.Services;

/// <summary>Un equipo aprobado en la pantalla de preparación, con su posición en el sorteo y sus problemas.</summary>
public record EquipoPreparacion(
    int IdEquipo,
    string Nombre,
    int Jugadores,
    int Posicion,
    IReadOnlyList<string> Problemas);

/// <summary>
/// Todo lo que la pantalla "Preparar calendario" necesita mostrar.
/// ProblemasGenerales: cosas que impiden iniciar y no son de un equipo concreto (por ejemplo, faltan equipos).
/// </summary>
public record PreparacionCalendario(
    Torneo Torneo,
    bool EsRegeneracion,
    bool IdaYVueltaActual,
    IReadOnlyList<EquipoPreparacion> Equipos,
    int PendientesARechazar,
    IReadOnlyList<string> ProblemasGenerales);

/// <summary>Posición (semilla) elegida para un equipo en el sorteo: 1 = primero.</summary>
public record PosicionEquipo(int IdEquipo, int Posicion);

/// <summary>
/// Resumen de lo que se generó al iniciar el torneo (para el mensaje de confirmación).
/// En una Copa, Jornadas son las rondas de la llave y PasanDirecto los equipos que avanzan sin jugar (bye).
/// </summary>
public record ResumenInicio(Torneo Torneo, int Jornadas, int Partidos, int Descansos, int EquiposRechazados, int PasanDirecto = 0);

/// <summary>Una jornada en el panel de gestión del calendario.</summary>
public record RondaGestion(
    int IdRonda,
    int Numero,
    string Nombre,
    DateOnly? FechaInicio,
    string NombreFase,
    int Partidos,
    int Descansos);

/// <summary>Datos del panel de gestión del calendario (solo organizador).</summary>
public record CalendarioGestion(
    Torneo Torneo,
    IReadOnlyList<RondaGestion> Rondas,
    bool PuedeRegenerar,
    string? MotivoNoRegenerar);

/// <summary>
/// Lógica de negocio del calendario (Paso 6, entregas A, B y C: base común + Liga + Copa).
/// Todas las operaciones son solo para el organizador del torneo (el torneo se identifica por su CÓDIGO).
/// </summary>
public interface IServicioCalendario
{
    /// <summary>
    /// Datos para la pantalla de preparación: equipos aprobados con sus problemas y posiciones iniciales
    /// (orden alfabético). Sirve para iniciar el torneo (estado Inscripción) o para regenerar el calendario
    /// (estado EnCurso, solo si todavía no se jugó ningún partido).
    /// </summary>
    Task<ResultadoServicio<PreparacionCalendario>> PrepararAsync(int idUsuario, string? codigoTorneo);

    /// <summary>Igual que PrepararAsync, pero con las posiciones sorteadas al azar (no guarda nada).</summary>
    Task<ResultadoServicio<PreparacionCalendario>> SortearAsync(int idUsuario, string? codigoTorneo);

    /// <summary>
    /// Inicia el torneo (o regenera el calendario): rechaza las solicitudes pendientes, pasa el torneo a EnCurso
    /// y crea la fase, los participantes, las jornadas y los partidos. Todo en una sola transacción.
    /// </summary>
    Task<ResultadoServicio<ResumenInicio>> IniciarAsync(
        int idUsuario, string? codigoTorneo, bool idaYVuelta, IReadOnlyList<PosicionEquipo> posiciones);

    /// <summary>Panel de gestión: las jornadas del torneo y si se puede regenerar el calendario.</summary>
    Task<ResultadoServicio<CalendarioGestion>> ObtenerGestionAsync(int idUsuario, string? codigoTorneo);

    /// <summary>Cambia el nombre y la fecha de inicio (opcional) de una jornada.</summary>
    Task<ResultadoServicio<Ronda>> EditarRondaAsync(
        int idUsuario, string? codigoTorneo, int idRonda, string? nombre, DateOnly? fechaInicio);
}
