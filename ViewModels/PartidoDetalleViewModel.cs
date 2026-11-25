using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.Services;

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

    /// <summary>Goles y tarjetas del partido, por minuto (los que no tienen minuto, al final). Son públicos.</summary>
    public List<EventoPartido> Eventos { get; set; } = new();

    /// <summary>true si este usuario puede registrar y eliminar eventos ahora (ver ReglasEvento.PuedeModificar).</summary>
    public bool PuedeModificarEventos { get; set; }

    /// <summary>Jugadores del equipo local, para el formulario de eventos (vacío si no se puede modificar).</summary>
    public List<Jugador> JugadoresLocal { get; set; } = new();

    /// <summary>Jugadores del equipo visitante, para el formulario de eventos (vacío si no se puede modificar).</summary>
    public List<Jugador> JugadoresVisitante { get; set; } = new();

    /// <summary>
    /// Datos de eliminatoria del partido (global, ganador, penales). Null si el partido no es de un cruce
    /// (liga o suizo). Los calcula ServicioEliminatoria.ObtenerInfoAsync.
    /// </summary>
    public InfoEliminatoria? Eliminatoria { get; set; }

    /// <summary>
    /// Historial de cambios de estado (el más reciente primero). Solo se llena para el organizador:
    /// para los demás el servicio no lo carga y esta lista queda vacía.
    /// </summary>
    public List<CambioEstadoPartido> Historial { get; set; } = new();
}
