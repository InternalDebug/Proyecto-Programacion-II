using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FairPlay.Models;

/// <summary>
/// Una fila de la tabla de goleadores de un torneo.   SQL: VISTA  vw_goleadores  (NO es una tabla).
///
/// - Es de SOLO LECTURA (entidad sin clave: HasNoKey().ToView("vw_goleadores") en el DbContext).
/// - Solo cuenta eventos de tipo Gol que tienen jugador asignado (los autogoles y los goles
///   sin jugador no suman a ningún goleador).
/// - La vista NO ordena: al consultar usar .OrderByDescending(g => g.Goles).
/// </summary>
public class Goleador
{
    /// <summary>Torneo en el que se anotaron los goles.</summary>
    [Column("id_torneo")]
    public int IdTorneo { get; set; }

    /// <summary>Jugador goleador.</summary>
    [Column("id_jugador")]
    public int IdJugador { get; set; }

    /// <summary>Nombre y apellido del jugador (alias "jugador" en la vista).</summary>
    [Display(Name = "Jugador")]
    [Column("jugador")]
    public string NombreJugador { get; set; } = string.Empty;

    /// <summary>Equipo del jugador.</summary>
    [Column("id_equipo")]
    public int IdEquipo { get; set; }

    /// <summary>Nombre del equipo (alias "equipo" en la vista).</summary>
    [Display(Name = "Equipo")]
    [Column("equipo")]
    public string NombreEquipo { get; set; } = string.Empty;

    /// <summary>Cantidad de goles anotados.</summary>
    [Display(Name = "Goles")]
    [Column("goles")]
    public int Goles { get; set; }
}
