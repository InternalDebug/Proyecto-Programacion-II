using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairPlay.Models.Enums;

namespace FairPlay.Models;

/// <summary>
/// Historial de auditoría: quién cambió el estado de un partido, de cuál a cuál y cuándo.
/// SQL: tabla  cambio_estado_partido.
/// Se inserta un registro cada vez que cambia partido.estado.
/// </summary>
[Table("cambio_estado_partido")]
public class CambioEstadoPartido : IValidatableObject
{
    /// <summary>SQL: id_cambio INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_cambio")]
    public int IdCambio { get; set; }

    /// <summary>SQL: id_partido INTEGER NOT NULL REFERENCES partido ON DELETE CASCADE.</summary>
    [Column("id_partido")]
    public int IdPartido { get; set; }

    /// <summary>SQL: id_usuario INTEGER NULL REFERENCES usuario ON DELETE SET NULL. Quién hizo el cambio.</summary>
    [Column("id_usuario")]
    public int? IdUsuario { get; set; }

    /// <summary>SQL: estado_anterior TEXT NOT NULL CHECK (IN ('Programado','EnVivo','Finalizado','Suspendido')).</summary>
    [Display(Name = "Estado anterior")]
    [Column("estado_anterior")]
    public EstadoPartido EstadoAnterior { get; set; }

    /// <summary>SQL: estado_nuevo TEXT NOT NULL CHECK (IN ('Programado','EnVivo','Finalizado','Suspendido')).</summary>
    [Display(Name = "Estado nuevo")]
    [Column("estado_nuevo")]
    public EstadoPartido EstadoNuevo { get; set; }

    /// <summary>
    /// SQL: fecha_hora TEXT NOT NULL DEFAULT (datetime('now')).
    /// UTC, formato "yyyy-MM-dd HH:mm:ss" (ver FechaHoraSqliteConverter en Data/Conversores.cs).
    /// </summary>
    [DataType(DataType.DateTime)]
    [Display(Name = "Fecha y hora")]
    [Column("fecha_hora")]
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Partido que cambió de estado (FK id_partido).</summary>
    [ForeignKey(nameof(IdPartido))]
    public Partido? Partido { get; set; }

    /// <summary>Usuario que realizó el cambio (FK id_usuario).</summary>
    [ForeignKey(nameof(IdUsuario))]
    public Usuario? Usuario { get; set; }

    /// <summary>SQL: CHECK (estado_anterior &lt;&gt; estado_nuevo).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EstadoAnterior == EstadoNuevo)
        {
            yield return new ValidationResult(
                "El estado nuevo debe ser distinto del estado anterior.",
                new[] { nameof(EstadoNuevo) });
        }
    }
}
