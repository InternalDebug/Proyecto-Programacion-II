using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairPlay.Models.Validaciones;

namespace FairPlay.Models;

/// <summary>
/// Jornada de liga, ronda suiza o ronda de eliminatoria (octavos, cuartos...).
/// SQL: tabla  ronda.
///
/// Reglas que impone la BD y que esta clase no puede comprobar sola:
///   - uq_ronda_numero: el número es único por fase y grupo
///     (índice único sobre (id_fase, IFNULL(id_grupo, 0), numero)).
///   - El grupo debe pertenecer a la MISMA fase que la ronda
///     (FOREIGN KEY (id_grupo, id_fase) REFERENCES grupo (id_grupo, id_fase)).
/// Si se rompen, SQLite lanza el error al guardar (DbUpdateException).
/// </summary>
[Table("ronda")]
public class Ronda
{
    /// <summary>SQL: id_ronda INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_ronda")]
    public int IdRonda { get; set; }

    /// <summary>SQL: id_fase INTEGER NOT NULL REFERENCES fase ON DELETE CASCADE.</summary>
    [Column("id_fase")]
    public int IdFase { get; set; }

    /// <summary>SQL: id_grupo INTEGER NULL (con FK compuesta a grupo). NULL = ronda de una fase sin grupos.</summary>
    [Column("id_grupo")]
    public int? IdGrupo { get; set; }

    /// <summary>SQL: numero INTEGER NOT NULL CHECK (numero &gt;= 1).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "El número de ronda debe ser 1 o mayor.")]
    [Display(Name = "Número")]
    [Column("numero")]
    public int Numero { get; set; }

    /// <summary>SQL: nombre TEXT NOT NULL CHECK (length(trim(nombre)) BETWEEN 1 AND 60). Ej. "Jornada 1", "Cuartos".</summary>
    [Required(ErrorMessage = "El nombre de la ronda es obligatorio.")]
    [LongitudRecortada(1, 60)]
    [Display(Name = "Nombre")]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>SQL: fecha_inicio TEXT NULL. Se guarda como 'yyyy-MM-dd' (DateOnly).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de inicio")]
    [Column("fecha_inicio")]
    public DateOnly? FechaInicio { get; set; }

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Fase a la que pertenece (FK id_fase).</summary>
    [ForeignKey(nameof(IdFase))]
    public Fase? Fase { get; set; }

    /// <summary>Grupo de la ronda, si la fase tiene grupos (FK id_grupo, ON DELETE CASCADE).</summary>
    [ForeignKey(nameof(IdGrupo))]
    public Grupo? Grupo { get; set; }

    /// <summary>Cruces de la ronda (cruce.id_ronda, ON DELETE CASCADE). Solo en eliminatorias.</summary>
    public ICollection<Cruce> Cruces { get; set; } = new List<Cruce>();

    /// <summary>Partidos de la ronda (partido.id_ronda, ON DELETE CASCADE).</summary>
    public ICollection<Partido> Partidos { get; set; } = new List<Partido>();
}
