using FairPlay.Models;
using FairPlay.Models.Enums;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos de la pantalla de detalle de un partido.
/// Los indicadores (Transiciones, PuedeEditarFecha...) los calcula el controlador con ReglasPartido
/// para que la vista solo muestre lo que ESTE usuario puede hacer. El servicio los vuelve a comprobar.
/// </summary>
public class PartidoDetalleViewModel
{
    public Partido Partido { get; set; } = null!;

    public Torneo Torneo { get; set; } = null!;

    public bool EsOrganizador { get; set; }

    public bool EsArbitro { get; set; }

    /// <summary>Estados a los que este usuario puede pasar el partido ahora (vacío = sin botones).</summary>
    public IReadOnlyList<EstadoPartido> Transiciones { get; set; } = Array.Empty<EstadoPartido>();

    /// <summary>true si el organizador puede cambiar la fecha y hora ahora.</summary>
    public bool PuedeEditarFecha { get; set; }

    /// <summary>true si el organizador puede designar o cambiar el árbitro ahora.</summary>
    public bool PuedeAsignarArbitro { get; set; }

    /// <summary>
    /// Historial de cambios de estado (el más reciente primero). Solo se llena para el organizador:
    /// para los demás el servicio no lo carga y esta lista queda vacía.
    /// </summary>
    public List<CambioEstadoPartido> Historial { get; set; } = new();
}
