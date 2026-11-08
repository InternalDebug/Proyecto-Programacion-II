using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FairPlay.Models;

/// <summary>
/// Enfrentamiento de una eliminatoria (tiene 1 partido, o 2 si la fase es ida y vuelta).
/// SQL: tabla  cruce   (UNIQUE (id_ronda, posicion)).
///
/// El "cuadro" del torneo se arma con id_cruce_siguiente: cada cruce apunta al cruce de la
/// ronda siguiente al que avanza su ganador.
/// </summary>
[Table("cruce")]
public class Cruce : IValidatableObject
{
    /// <summary>SQL: id_cruce INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_cruce")]
    public int IdCruce { get; set; }

    /// <summary>SQL: id_ronda INTEGER NOT NULL REFERENCES ronda ON DELETE CASCADE.</summary>
    [Column("id_ronda")]
    public int IdRonda { get; set; }

    /// <summary>SQL: posicion INTEGER NOT NULL CHECK (posicion &gt;= 1). Lugar del cruce dentro de la ronda.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "La posición debe ser 1 o mayor.")]
    [Display(Name = "Posición")]
    [Column("posicion")]
    public int Posicion { get; set; }

    /// <summary>SQL: id_equipo_a INTEGER NULL REFERENCES equipo ON DELETE NO ACTION. NULL = aún por definir.</summary>
    [Column("id_equipo_a")]
    public int? IdEquipoA { get; set; }

    /// <summary>SQL: id_equipo_b INTEGER NULL REFERENCES equipo ON DELETE NO ACTION. NULL = aún por definir.</summary>
    [Column("id_equipo_b")]
    public int? IdEquipoB { get; set; }

    /// <summary>SQL: id_ganador INTEGER NULL REFERENCES equipo ON DELETE NO ACTION. NULL = sin ganador todavía.</summary>
    [Column("id_ganador")]
    public int? IdGanador { get; set; }

    /// <summary>SQL: id_cruce_siguiente INTEGER NULL REFERENCES cruce ON DELETE SET NULL (auto-referencia).</summary>
    [Column("id_cruce_siguiente")]
    public int? IdCruceSiguiente { get; set; }

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Ronda del cruce (FK id_ronda).</summary>
    [ForeignKey(nameof(IdRonda))]
    public Ronda? Ronda { get; set; }

    /// <summary>Primer equipo del cruce (FK id_equipo_a).</summary>
    [ForeignKey(nameof(IdEquipoA))]
    public Equipo? EquipoA { get; set; }

    /// <summary>Segundo equipo del cruce (FK id_equipo_b).</summary>
    [ForeignKey(nameof(IdEquipoB))]
    public Equipo? EquipoB { get; set; }

    /// <summary>Equipo que ganó el cruce (FK id_ganador).</summary>
    [ForeignKey(nameof(IdGanador))]
    public Equipo? Ganador { get; set; }

    /// <summary>Cruce de la ronda siguiente al que avanza el ganador (FK id_cruce_siguiente).</summary>
    [ForeignKey(nameof(IdCruceSiguiente))]
    public Cruce? CruceSiguiente { get; set; }

    /// <summary>Cruces que alimentan a este (lado inverso de CruceSiguiente).</summary>
    public ICollection<Cruce> CrucesPrevios { get; set; } = new List<Cruce>();

    /// <summary>Partido(s) del cruce: 1 (partido único) o 2 (ida y vuelta).</summary>
    public ICollection<Partido> Partidos { get; set; } = new List<Partido>();

    // ------------------------------------------------------------------
    // Reglas de tabla del SQL
    // ------------------------------------------------------------------

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // SQL: CHECK (id_equipo_a <> id_equipo_b)   (si alguno es NULL, el CHECK se cumple)
        if (IdEquipoA.HasValue && IdEquipoB.HasValue && IdEquipoA.Value == IdEquipoB.Value)
        {
            yield return new ValidationResult(
                "Un equipo no puede enfrentarse a sí mismo.",
                new[] { nameof(IdEquipoB) });
        }

        // SQL: CHECK (id_ganador IS NULL OR id_ganador = id_equipo_a OR id_ganador = id_equipo_b)
        if (IdGanador.HasValue && IdGanador != IdEquipoA && IdGanador != IdEquipoB)
        {
            yield return new ValidationResult(
                "El ganador debe ser uno de los dos equipos del cruce.",
                new[] { nameof(IdGanador) });
        }

        // SQL: CHECK (id_cruce_siguiente IS NULL OR id_cruce_siguiente <> id_cruce)
        if (IdCruceSiguiente.HasValue && IdCruceSiguiente.Value == IdCruce)
        {
            yield return new ValidationResult(
                "Un cruce no puede ser su propio cruce siguiente.",
                new[] { nameof(IdCruceSiguiente) });
        }
    }
}
