using FairPlay.Models;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos de la lista de partidos de un torneo (GET /Torneos/{código}/Partidos).
/// Los indicadores sirven para mostrar (o no) enlaces en la vista; la seguridad real NO depende de
/// ellos: el servicio vuelve a comprobar el permiso en cada operación.
/// </summary>
public class PartidosTorneoViewModel
{
    public Torneo Torneo { get; set; } = null!;

    /// <summary>Partidos ya ordenados por fase, ronda y partido (ver IServicioPartidos.ListarDeTorneoAsync).</summary>
    public List<Partido> Partidos { get; set; } = new();

    /// <summary>true si el usuario con sesión iniciada es el organizador del torneo.</summary>
    public bool EsOrganizador { get; set; }
}
