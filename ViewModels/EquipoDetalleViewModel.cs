using FairPlay.Models;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos de la página de detalle de un equipo (con sus jugadores).
/// La vista usa los indicadores para mostrar u ocultar botones; la seguridad real NO depende
/// de esto: el servicio vuelve a comprobar el permiso en cada operación.
/// </summary>
public class EquipoDetalleViewModel
{
    /// <summary>El equipo, con Torneo, Responsable y Jugadores cargados.</summary>
    public Equipo Equipo { get; set; } = null!;

    /// <summary>true si el usuario con sesión es el organizador del torneo del equipo.</summary>
    public bool EsOrganizador { get; set; }

    /// <summary>true si el usuario con sesión es quien inscribió el equipo.</summary>
    public bool EsResponsable { get; set; }

    /// <summary>Puede editar/eliminar el equipo y agregar, editar o eliminar sus jugadores.</summary>
    public bool PuedeGestionar => EsOrganizador || EsResponsable;
}
