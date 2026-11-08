using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FairPlay.Models;

/// <summary>
/// Qué equipos juegan cada fase, en qué grupo y si clasificaron a la siguiente.
/// SQL: tabla  participante_fase   (UNIQUE (id_fase, id_equipo)).
///
/// Trigger de la BD (trg_participante_grupo_ins/upd): el grupo debe pertenecer a la MISMA
/// fase que el participante; si no, SQLite aborta el guardado.
/// </summary>
[Table("participante_fase")]
public class ParticipanteFase
{
    /// <summary>SQL: id_participante INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_participante")]
    public int IdParticipante { get; set; }

    /// <summary>SQL: id_fase INTEGER NOT NULL REFERENCES fase ON DELETE CASCADE.</summary>
    [Column("id_fase")]
    public int IdFase { get; set; }

    /// <summary>SQL: id_equipo INTEGER NOT NULL REFERENCES equipo ON DELETE NO ACTION.</summary>
    [Column("id_equipo")]
    public int IdEquipo { get; set; }

    /// <summary>SQL: id_grupo INTEGER NULL REFERENCES grupo ON DELETE SET NULL. NULL = fase sin grupos.</summary>
    [Column("id_grupo")]
    public int? IdGrupo { get; set; }

    /// <summary>SQL: semilla INTEGER NULL CHECK (semilla IS NULL OR semilla &gt;= 1).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "La semilla debe ser 1 o mayor.")]
    [Display(Name = "Semilla")]
    [Column("semilla")]
    public int? Semilla { get; set; }

    /// <summary>SQL: clasificado INTEGER NOT NULL DEFAULT 0 CHECK (IN (0,1)). ¿Pasó a la siguiente fase?</summary>
    [Display(Name = "Clasificado")]
    [Column("clasificado")]
    public bool Clasificado { get; set; }

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Fase que juega (FK id_fase).</summary>
    [ForeignKey(nameof(IdFase))]
    public Fase? Fase { get; set; }

    /// <summary>Equipo participante (FK id_equipo).</summary>
    [ForeignKey(nameof(IdEquipo))]
    public Equipo? Equipo { get; set; }

    /// <summary>Grupo asignado, si lo hay (FK id_grupo).</summary>
    [ForeignKey(nameof(IdGrupo))]
    public Grupo? Grupo { get; set; }
}
