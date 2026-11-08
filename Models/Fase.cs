using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairPlay.Models.Enums;
using FairPlay.Models.Validaciones;

namespace FairPlay.Models;

/// <summary>
/// Fase de un torneo.   SQL: tabla  fase.
///
///   Liga       -> 1 fase Liga
///   Copa       -> 1 fase Eliminatoria
///   GruposCopa -> fase 1 Liga (con grupos) + fase 2 Eliminatoria
///   Suizo      -> 1 fase Suizo
///
/// Implementa IValidatableObject para los dos CHECK de tabla que relacionan columnas entre sí.
/// </summary>
[Table("fase")]
public class Fase : IValidatableObject
{
    /// <summary>SQL: id_fase INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_fase")]
    public int IdFase { get; set; }

    /// <summary>SQL: id_torneo INTEGER NOT NULL REFERENCES torneo ON DELETE CASCADE.</summary>
    [Column("id_torneo")]
    public int IdTorneo { get; set; }

    /// <summary>SQL: orden INTEGER NOT NULL CHECK (orden &gt;= 1); UNIQUE (id_torneo, orden).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "El orden debe ser 1 o mayor.")]
    [Display(Name = "Orden")]
    [Column("orden")]
    public int Orden { get; set; }

    /// <summary>SQL: nombre TEXT NOT NULL CHECK (length(trim(nombre)) BETWEEN 1 AND 100).</summary>
    [Required(ErrorMessage = "El nombre de la fase es obligatorio.")]
    [LongitudRecortada(1, 100)]
    [Display(Name = "Nombre")]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>SQL: tipo TEXT NOT NULL CHECK (tipo IN ('Liga','Eliminatoria','Suizo')).</summary>
    [Display(Name = "Tipo de fase")]
    [Column("tipo")]
    public TipoFase Tipo { get; set; }

    /// <summary>SQL: ida_y_vuelta INTEGER NOT NULL DEFAULT 0 CHECK (IN (0,1)). bool = 0/1 en SQLite.</summary>
    [Display(Name = "Ida y vuelta")]
    [Column("ida_y_vuelta")]
    public bool IdaYVuelta { get; set; }

    /// <summary>
    /// SQL: num_rondas INTEGER NULL CHECK (num_rondas IS NULL OR num_rondas &gt;= 1).
    /// Solo se usa (y es obligatorio) en fases Suizo; en las demás debe ser NULL.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "El número de rondas debe ser 1 o mayor.")]
    [Display(Name = "Número de rondas")]
    [Column("num_rondas")]
    public int? NumRondas { get; set; }

    /// <summary>
    /// SQL: clasifican_por_grupo INTEGER NULL CHECK (IS NULL OR &gt;= 1).
    /// Cuántos equipos de cada grupo pasan a la siguiente fase (formato GruposCopa).
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Debe clasificar al menos un equipo por grupo.")]
    [Display(Name = "Clasifican por grupo")]
    [Column("clasifican_por_grupo")]
    public int? ClasificanPorGrupo { get; set; }


    /// <summary>SQL: estado TEXT NOT NULL DEFAULT 'Pendiente' (Pendiente/EnCurso/Cerrada).</summary>
    [Display(Name = "Estado")]
    [Column("estado")]
    public EstadoFase Estado { get; set; } = EstadoFase.Pendiente;

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Torneo al que pertenece (FK id_torneo).</summary>
    [ForeignKey(nameof(IdTorneo))]
    public Torneo? Torneo { get; set; }

    /// <summary>Grupos de la fase (grupo.id_fase, ON DELETE CASCADE).</summary>
    public ICollection<Grupo> Grupos { get; set; } = new List<Grupo>();

    /// <summary>Equipos que juegan esta fase (participante_fase.id_fase, ON DELETE CASCADE).</summary>
    public ICollection<ParticipanteFase> Participantes { get; set; } = new List<ParticipanteFase>();

    /// <summary>Rondas/jornadas de la fase (ronda.id_fase, ON DELETE CASCADE).</summary>
    public ICollection<Ronda> Rondas { get; set; } = new List<Ronda>();

    // ------------------------------------------------------------------
    // Reglas de tabla del SQL
    // ------------------------------------------------------------------

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // SQL: CHECK ((tipo = 'Suizo') = (num_rondas IS NOT NULL))  -> num_rondas solo en Suizo
        if (Tipo == TipoFase.Suizo && !NumRondas.HasValue)
        {
            yield return new ValidationResult(
                "Una fase de tipo Suizo debe indicar el número de rondas.",
                new[] { nameof(NumRondas) });
        }

        if (Tipo != TipoFase.Suizo && NumRondas.HasValue)
        {
            yield return new ValidationResult(
                "El número de rondas solo se usa en las fases de tipo Suizo.",
                new[] { nameof(NumRondas) });
        }

        // SQL: CHECK (tipo <> 'Suizo' OR ida_y_vuelta = 0)  -> en el suizo nunca se repite rival
        if (Tipo == TipoFase.Suizo && IdaYVuelta)
        {
            yield return new ValidationResult(
                "El sistema suizo no puede ser de ida y vuelta (nunca se repite el rival).",
                new[] { nameof(IdaYVuelta) });
        }
    }
}
