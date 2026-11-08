using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairPlay.Models.Enums;

namespace FairPlay.Models;

/// <summary>
/// Partido de un torneo.   SQL: tabla  partido.
///
/// Casos especiales:
///   - id_visitante NULL = el equipo local DESCANSA (liga con número impar de equipos o "bye" suizo).
///     Un descanso suizo se guarda con estado Finalizado y goles NULL.
///   - Los goles son NULL mientras el partido está Programado.
///   - penales_* solo se usan para definir un cruce de eliminatoria empatado.
///
/// Triggers de la BD (la app solo debe mostrar opciones válidas; si no, SQLite aborta el guardado):
///   - trg_partido_transicion : máquina de estados (ver TransicionPermitida).
///   - trg_partido_equipos_*  : los equipos deben pertenecer al torneo de la ronda.
///
/// Los PERMISOS (quién puede cambiar el estado o registrar goles) se validan en la aplicación:
/// el árbitro solo modifica el partido cuando está EnVivo y NO puede reabrir un Finalizado.
/// </summary>
[Table("partido")]
public class Partido : IValidatableObject
{
    /// <summary>SQL: id_partido INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_partido")]
    public int IdPartido { get; set; }

    /// <summary>SQL: id_ronda INTEGER NOT NULL REFERENCES ronda ON DELETE CASCADE.</summary>
    [Column("id_ronda")]
    public int IdRonda { get; set; }

    /// <summary>SQL: id_cruce INTEGER NULL REFERENCES cruce ON DELETE CASCADE. NULL = partido de liga/suizo.</summary>
    [Column("id_cruce")]
    public int? IdCruce { get; set; }

    /// <summary>SQL: id_arbitro INTEGER NULL REFERENCES usuario ON DELETE SET NULL.</summary>
    [Display(Name = "Árbitro")]
    [Column("id_arbitro")]
    public int? IdArbitro { get; set; }

    /// <summary>
    /// SQL: numero_partido INTEGER NOT NULL DEFAULT 1 CHECK (numero_partido IN (1, 2));
    ///      UNIQUE (id_cruce, numero_partido).   1 = ida, 2 = vuelta.
    /// </summary>
    [Range(1, 2, ErrorMessage = "El número de partido debe ser 1 (ida) o 2 (vuelta).")]
    [Display(Name = "Partido (1 = ida, 2 = vuelta)")]
    [Column("numero_partido")]
    public int NumeroPartido { get; set; } = 1;

    /// <summary>SQL: id_local INTEGER NOT NULL REFERENCES equipo ON DELETE NO ACTION.</summary>
    [Display(Name = "Local")]
    [Column("id_local")]
    public int IdLocal { get; set; }

    /// <summary>SQL: id_visitante INTEGER NULL REFERENCES equipo ON DELETE NO ACTION. NULL = el local descansa.</summary>
    [Display(Name = "Visitante")]
    [Column("id_visitante")]
    public int? IdVisitante { get; set; }

    /// <summary>
    /// SQL: fecha_hora TEXT NULL. Se guarda en ISO-8601 con 'T' ("2026-10-26T15:30:00"),
    /// ver FechaHoraIsoConverter en Data/Conversores.cs.
    /// </summary>
    [DataType(DataType.DateTime)]
    [Display(Name = "Fecha y hora")]
    [Column("fecha_hora")]
    public DateTime? FechaHora { get; set; }

    /// <summary>SQL: estado TEXT NOT NULL DEFAULT 'Programado' (Programado/EnVivo/Finalizado/Suspendido).</summary>
    [Display(Name = "Estado")]
    [Column("estado")]
    public EstadoPartido Estado { get; set; } = EstadoPartido.Programado;

    /// <summary>SQL: goles_local INTEGER NULL CHECK (goles_local IS NULL OR goles_local &gt;= 0).</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Los goles no pueden ser negativos.")]
    [Display(Name = "Goles local")]
    [Column("goles_local")]
    public int? GolesLocal { get; set; }

    /// <summary>SQL: goles_visitante INTEGER NULL CHECK (goles_visitante IS NULL OR goles_visitante &gt;= 0).</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Los goles no pueden ser negativos.")]
    [Display(Name = "Goles visitante")]
    [Column("goles_visitante")]
    public int? GolesVisitante { get; set; }

    /// <summary>SQL: penales_local INTEGER NULL CHECK (penales_local IS NULL OR penales_local &gt;= 0).</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Los penales no pueden ser negativos.")]
    [Display(Name = "Penales local")]
    [Column("penales_local")]
    public int? PenalesLocal { get; set; }

    /// <summary>SQL: penales_visitante INTEGER NULL CHECK (penales_visitante IS NULL OR penales_visitante &gt;= 0).</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Los penales no pueden ser negativos.")]
    [Display(Name = "Penales visitante")]
    [Column("penales_visitante")]
    public int? PenalesVisitante { get; set; }

    /// <summary>True si el local descansa (no hay visitante). No es una columna.</summary>
    [NotMapped]
    public bool EsDescanso => IdVisitante == null;

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Ronda del partido (FK id_ronda).</summary>
    [ForeignKey(nameof(IdRonda))]
    public Ronda? Ronda { get; set; }

    /// <summary>Cruce al que pertenece, si es de eliminatoria (FK id_cruce).</summary>
    [ForeignKey(nameof(IdCruce))]
    public Cruce? Cruce { get; set; }

    /// <summary>Usuario designado como árbitro (FK id_arbitro).</summary>
    [ForeignKey(nameof(IdArbitro))]
    public Usuario? Arbitro { get; set; }

    /// <summary>Equipo local (FK id_local).</summary>
    [ForeignKey(nameof(IdLocal))]
    public Equipo? Local { get; set; }

    /// <summary>Equipo visitante (FK id_visitante). Null si el local descansa.</summary>
    [ForeignKey(nameof(IdVisitante))]
    public Equipo? Visitante { get; set; }

    /// <summary>Historial de cambios de estado (cambio_estado_partido.id_partido, ON DELETE CASCADE).</summary>
    public ICollection<CambioEstadoPartido> CambiosEstado { get; set; } = new List<CambioEstadoPartido>();

    /// <summary>Goles y tarjetas del partido (evento_partido.id_partido, ON DELETE CASCADE).</summary>
    public ICollection<EventoPartido> Eventos { get; set; } = new List<EventoPartido>();

    // ------------------------------------------------------------------
    // Reglas del SQL
    // ------------------------------------------------------------------

    /// <summary>
    /// Copia en C# de la máquina de estados del trigger trg_partido_transicion, para avisar al
    /// usuario con un mensaje claro ANTES de que SQLite aborte el UPDATE.
    ///   Programado -> EnVivo | Suspendido
    ///   EnVivo     -> Finalizado | Suspendido
    ///   Finalizado -> EnVivo            (reabrir)
    ///   Suspendido -> Programado
    /// Mantener el mismo estado siempre es válido (el trigger solo actúa si el estado cambia).
    /// OJO: esto es solo la regla de DATOS. La regla de PERMISOS (el árbitro no puede pasar de
    /// Finalizado a EnVivo; solo el organizador) se aplica en el controlador o servicio.
    /// </summary>
    public static bool TransicionPermitida(EstadoPartido actual, EstadoPartido nuevo)
    {
        if (actual == nuevo)
        {
            return true;
        }

        return (actual == EstadoPartido.Programado && (nuevo == EstadoPartido.EnVivo || nuevo == EstadoPartido.Suspendido))
            || (actual == EstadoPartido.EnVivo && (nuevo == EstadoPartido.Finalizado || nuevo == EstadoPartido.Suspendido))
            || (actual == EstadoPartido.Finalizado && nuevo == EstadoPartido.EnVivo)
            || (actual == EstadoPartido.Suspendido && nuevo == EstadoPartido.Programado);
    }

    /// <summary>Los CHECK de tabla de partido (todos relacionan varias columnas).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // SQL: CHECK (id_local <> id_visitante)
        if (IdVisitante.HasValue && IdLocal == IdVisitante.Value)
        {
            yield return new ValidationResult(
                "El equipo local y el visitante deben ser distintos.",
                new[] { nameof(IdVisitante) });
        }

        // SQL: CHECK ((penales_local IS NULL) = (penales_visitante IS NULL))
        if (PenalesLocal.HasValue != PenalesVisitante.HasValue)
        {
            yield return new ValidationResult(
                "Los penales de ambos equipos deben registrarse juntos (o ninguno).",
                new[] { nameof(PenalesLocal), nameof(PenalesVisitante) });
        }

        // SQL: CHECK ((goles_local IS NULL) = (goles_visitante IS NULL))
        if (GolesLocal.HasValue != GolesVisitante.HasValue)
        {
            yield return new ValidationResult(
                "Los goles de ambos equipos deben registrarse juntos (o ninguno).",
                new[] { nameof(GolesLocal), nameof(GolesVisitante) });
        }

        // SQL: CHECK (id_visitante IS NULL OR estado NOT IN ('EnVivo','Finalizado') OR goles_local IS NOT NULL)
        if (IdVisitante.HasValue
            && (Estado == EstadoPartido.EnVivo || Estado == EstadoPartido.Finalizado)
            && !GolesLocal.HasValue)
        {
            yield return new ValidationResult(
                "Un partido En vivo o Finalizado debe tener marcador (empieza en 0 - 0).",
                new[] { nameof(GolesLocal), nameof(GolesVisitante) });
        }

        // SQL: CHECK (id_visitante IS NOT NULL OR (goles_local IS NULL AND penales_local IS NULL))
        if (!IdVisitante.HasValue && (GolesLocal.HasValue || PenalesLocal.HasValue))
        {
            yield return new ValidationResult(
                "Un descanso (sin visitante) no puede tener goles ni penales.",
                new[] { nameof(IdVisitante) });
        }
    }
}
