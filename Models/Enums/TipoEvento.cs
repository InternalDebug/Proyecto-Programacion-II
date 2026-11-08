namespace FairPlay.Models.Enums;

/// <summary>
/// Tipo de evento ocurrido durante un partido.
/// SQL: evento_partido.tipo  CHECK (tipo IN ('Gol', 'Autogol', 'Amarilla', 'Roja')).
/// </summary>
public enum TipoEvento
{
    /// <summary>
    /// Gol. Es el único evento que puede registrarse SIN jugador (el árbitro solo suma al marcador);
    /// en ese caso id_equipo es el equipo al que se acredita el gol.
    /// </summary>
    Gol,

    /// <summary>Autogol: id_equipo es el equipo del jugador; el marcador lo suma al RIVAL.</summary>
    Autogol,

    /// <summary>Tarjeta amarilla (exige jugador).</summary>
    Amarilla,

    /// <summary>Tarjeta roja (exige jugador).</summary>
    Roja
}
