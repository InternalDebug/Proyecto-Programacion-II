using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairPlay.Models.Validaciones;

namespace FairPlay.Models;

/// <summary>
/// Grupo dentro de una fase de liga (ej. "Grupo A").   SQL: tabla  grupo.
/// Solo existe en torneos de formato GruposCopa.
/// </summary>
[Table("grupo")]
public class Grupo
{
    /// <summary>SQL: id_grupo INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_grupo")]
    public int IdGrupo { get; set; }

    /// <summary>SQL: id_fase INTEGER NOT NULL REFERENCES fase ON DELETE CASCADE.</summary>
    [Column("id_fase")]
    public int IdFase { get; set; }

    /// <summary>
    /// SQL: nombre TEXT NOT NULL CHECK (length(trim(nombre)) BETWEEN 1 AND 50);
    ///      UNIQUE (id_fase, nombre).
    /// </summary>
    [Required(ErrorMessage = "El nombre del grupo es obligatorio.")]
    [LongitudRecortada(1, 50)]
    [Display(Name = "Nombre")]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Fase a la que pertenece (FK id_fase).</summary>
    [ForeignKey(nameof(IdFase))]
    public Fase? Fase { get; set; }

    /// <summary>Equipos del grupo (participante_fase.id_grupo, ON DELETE SET NULL).</summary>
    public ICollection<ParticipanteFase> Participantes { get; set; } = new List<ParticipanteFase>();

    /// <summary>Jornadas del grupo (ronda.id_grupo, ON DELETE CASCADE).</summary>
    public ICollection<Ronda> Rondas { get; set; } = new List<Ronda>();
}
