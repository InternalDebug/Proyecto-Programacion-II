using FairPlay.Models;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos de la página de detalle de un torneo.
/// Lleva, además del torneo, si quien lo mira es su organizador: la vista usa ese dato
/// para mostrar (o no) los botones Editar y Eliminar. La seguridad real NO depende de
/// esto: el servicio vuelve a comprobar el permiso en cada operación.
/// </summary>
public class TorneoDetalleViewModel
{
    public Torneo Torneo { get; set; } = null!;

    /// <summary>true si el usuario con sesión iniciada es el organizador de este torneo.</summary>
    public bool EsOrganizador { get; set; }
}
