using FairPlay.Models;

namespace FairPlay.ViewModels;

/// <summary>Datos de la pantalla "Llave" de un torneo con eliminatoria (pública para quien conozca el código).</summary>
public class LlaveViewModel
{
    public Torneo Torneo { get; set; } = null!;

    /// <summary>Rondas de la eliminatoria (de la primera a la final), cada una con sus cruces, equipos y partidos.</summary>
    public List<Ronda> Rondas { get; set; } = new();

    /// <summary>Ganador de la final, si ya se decidió (null mientras el torneo sigue en juego).</summary>
    public Equipo? Campeon { get; set; }
}

/// <summary>
/// Lo que envía el formulario de penales (campos PenalesLocal y PenalesVisitante). Los dos vacíos significan
/// "quitar los penales". Las reglas (rango, sin empate) las valida el servicio, no la pantalla.
/// </summary>
public class PenalesFormViewModel
{
    public int? PenalesLocal { get; set; }

    public int? PenalesVisitante { get; set; }
}
