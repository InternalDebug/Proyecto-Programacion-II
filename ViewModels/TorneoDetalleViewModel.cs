using FairPlay.Models;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos de la página de detalle de un torneo.
/// Los indicadores sirven para mostrar (o no) botones en la vista. La seguridad real NO depende
/// de ellos: los servicios vuelven a comprobar el permiso en cada operación.
/// </summary>
public class TorneoDetalleViewModel
{
    public Torneo Torneo { get; set; } = null!;

    /// <summary>true si el usuario con sesión iniciada es el organizador de este torneo.</summary>
    public bool EsOrganizador { get; set; }

    /// <summary>
    /// Equipos que ESTE usuario puede ver: los Aprobados para cualquiera; además, los Pendientes y
    /// Rechazados para el organizador, y los propios para su responsable.
    /// </summary>
    public List<Equipo> Equipos { get; set; } = new();

    /// <summary>true si el usuario puede inscribir un equipo ahora (según modo de inscripción, estado y cupo).</summary>
    public bool PuedeInscribir { get; set; }

    /// <summary>
    /// true si el organizador marcó "No mostrar de nuevo" en la pantalla informativa del cambio de
    /// estado: entonces el botón cambia el estado directamente, sin mostrar esa pantalla.
    /// </summary>
    public bool OmitirAvisoCambioEstado { get; set; }
}
