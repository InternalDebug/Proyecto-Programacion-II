namespace FairPlay.Services;

/// <summary>
/// Datos para el enlace "Mis partidos" de la barra de navegación (lo dibuja PartidosNavViewComponent).
/// </summary>
/// <param name="MostrarEnMenu">true si el usuario es árbitro designado en al menos un partido.</param>
/// <param name="EnVivo">Cuántos de sus partidos se están jugando ahora (globo verde).</param>
/// <param name="PorJugar">Cuántos de sus partidos todavía están Programados.</param>
public record ResumenArbitraje(bool MostrarEnMenu, int EnVivo, int PorJugar);
