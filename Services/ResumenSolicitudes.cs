namespace FairPlay.Services;

/// <summary>
/// Datos para el enlace "Solicitudes de inscripción" de la barra de navegación.
/// </summary>
/// <param name="MostrarEnMenu">
/// true si el usuario organiza algún torneo en Inscripción con modo ConAprobación,
/// o si tiene solicitudes pendientes por resolver.
/// </param>
/// <param name="Pendientes">Cuántos equipos están esperando una decisión (aparece como globo con el número).</param>
public record ResumenSolicitudes(bool MostrarEnMenu, int Pendientes);
