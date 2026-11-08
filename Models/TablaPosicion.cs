using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FairPlay.Models;

/// <summary>
/// Una fila de la tabla de posiciones.   SQL: VISTA  vw_tabla_posiciones  (NO es una tabla).
///
/// - Es de SOLO LECTURA: no se hace Add/Update/Remove sobre ella. En el DbContext se mapea
///   como entidad sin clave con HasNoKey().ToView("vw_tabla_posiciones").
/// - Solo cuenta partidos Finalizado. Los puntos salen de torneo.puntos_victoria / empate / derrota.
/// - En fases Suizo, un descanso (bye) cuenta como victoria sin goles.
/// - id_grupo NULL = fase sin grupos.
/// - La vista NO ordena. Al consultar hay que ordenar:
///       .OrderByDescending(t => t.Puntos).ThenByDescending(t => t.Dg).ThenByDescending(t => t.Gf)
///   (equivale a ORDER BY puntos DESC, dg DESC, gf DESC).
/// </summary>
public class TablaPosicion
{
    /// <summary>Fase a la que pertenece la fila.</summary>
    [Column("id_fase")]
    public int IdFase { get; set; }

    /// <summary>Grupo (NULL si la fase no tiene grupos).</summary>
    [Column("id_grupo")]
    public int? IdGrupo { get; set; }

    /// <summary>Equipo de la fila.</summary>
    [Column("id_equipo")]
    public int IdEquipo { get; set; }

    /// <summary>Nombre del equipo (alias "equipo" en la vista).</summary>
    [Display(Name = "Equipo")]
    [Column("equipo")]
    public string NombreEquipo { get; set; } = string.Empty;

    /// <summary>Partidos jugados.</summary>
    [Display(Name = "PJ")]
    [Column("pj")]
    public int Pj { get; set; }

    /// <summary>Partidos ganados.</summary>
    [Display(Name = "PG")]
    [Column("pg")]
    public int Pg { get; set; }

    /// <summary>Partidos empatados.</summary>
    [Display(Name = "PE")]
    [Column("pe")]
    public int Pe { get; set; }

    /// <summary>Partidos perdidos.</summary>
    [Display(Name = "PP")]
    [Column("pp")]
    public int Pp { get; set; }

    /// <summary>Goles a favor.</summary>
    [Display(Name = "GF")]
    [Column("gf")]
    public int Gf { get; set; }

    /// <summary>Goles en contra.</summary>
    [Display(Name = "GC")]
    [Column("gc")]
    public int Gc { get; set; }

    /// <summary>Diferencia de goles (gf - gc). Puede ser negativa.</summary>
    [Display(Name = "DG")]
    [Column("dg")]
    public int Dg { get; set; }

    /// <summary>Puntos totales según la configuración de puntos del torneo.</summary>
    [Display(Name = "Pts")]
    [Column("puntos")]
    public int Puntos { get; set; }
}
