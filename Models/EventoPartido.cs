using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairPlay.Models.Enums;

namespace FairPlay.Models;

/// <summary>
/// Gol, autogol o tarjeta ocurrido durante un partido.   SQL: tabla  evento_partido.
///
/// Trigger de la BD (trg_evento_integridad_ins/upd): el equipo debe jugar ese partido y el
/// jugador debe pertenecer a ese equipo. La app debe ofrecer solo equipos y jugadores válidos.
///
/// Significado de id_equipo según el tipo:
///   Gol (con jugador) -> equipo del jugador que anotó.
///   Gol (sin jugador) -> equipo al que se acredita (el árbitro solo suma al marcador).
///   Autogol           -> equipo del jugador; el marcador lo suma al RIVAL.
///   Amarilla / Roja   -> equipo del jugador sancionado.
/// </summary>
[Table("evento_partido")]
public class EventoPartido : IValidatableObject
{
    /// <summary>SQL: id_evento INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_evento")]
    public int IdEvento { get; set; }

    /// <summary>SQL: id_partido INTEGER NOT NULL REFERENCES partido ON DELETE CASCADE.</summary>
    [Column("id_partido")]
    public int IdPartido { get; set; }

    /// <summary>SQL: id_equipo INTEGER NOT NULL REFERENCES equipo ON DELETE NO ACTION.</summary>
    [Display(Name = "Equipo")]
    [Column("id_equipo")]
    public int IdEquipo { get; set; }

    /// <summary>SQL: id_jugador INTEGER NULL REFERENCES jugador ON DELETE NO ACTION. Solo un Gol puede ir sin jugador.</summary>
    [Display(Name = "Jugador")]
    [Column("id_jugador")]
    public int? IdJugador { get; set; }

    /// <summary>SQL: id_registrado_por INTEGER NULL REFERENCES usuario ON DELETE SET NULL. Quién lo anotó (árbitro).</summary>
    [Column("id_registrado_por")]
    public int? IdRegistradoPor { get; set; }

    /// <summary>SQL: tipo TEXT NOT NULL CHECK (tipo IN ('Gol','Autogol','Amarilla','Roja')).</summary>
    [Display(Name = "Tipo de evento")]
    [Column("tipo")]
    public TipoEvento Tipo { get; set; }

    /// <summary>SQL: minuto INTEGER NULL CHECK (minuto IS NULL OR minuto BETWEEN 0 AND 130).</summary>
    [Range(0, 130, ErrorMessage = "El minuto debe estar entre {1} y {2}.")]
    [Display(Name = "Minuto")]
    [Column("minuto")]
    public int? Minuto { get; set; }

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Partido donde ocurrió (FK id_partido).</summary>
    [ForeignKey(nameof(IdPartido))]
    public Partido? Partido { get; set; }

    /// <summary>Equipo del evento (FK id_equipo).</summary>
    [ForeignKey(nameof(IdEquipo))]
    public Equipo? Equipo { get; set; }

    /// <summary>Jugador protagonista, si lo hay (FK id_jugador).</summary>
    [ForeignKey(nameof(IdJugador))]
    public Jugador? Jugador { get; set; }

    /// <summary>Usuario que registró el evento (FK id_registrado_por).</summary>
    [ForeignKey(nameof(IdRegistradoPor))]
    public Usuario? RegistradoPor { get; set; }

    /// <summary>SQL: CHECK (tipo = 'Gol' OR id_jugador IS NOT NULL).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Tipo != TipoEvento.Gol && !IdJugador.HasValue)
        {
            yield return new ValidationResult(
                "Autogoles y tarjetas deben indicar el jugador.",
                new[] { nameof(IdJugador) });
        }
    }
}
