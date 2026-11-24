using FairPlay.Models.Enums;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos de la pantalla informativa que aparece al abrir o cerrar las inscripciones de un torneo.
/// </summary>
public class CambioEstadoViewModel
{
    public string Codigo { get; set; } = string.Empty;

    public string NombreTorneo { get; set; } = string.Empty;

    public ModoInscripcion Modo { get; set; }

    /// <summary>Estado al que pasará el torneo si el organizador confirma.</summary>
    public EstadoTorneo NuevoEstado { get; set; }
}
