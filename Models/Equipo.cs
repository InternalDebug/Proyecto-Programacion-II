using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairPlay.Models.Enums;
using FairPlay.Models.Validaciones;

namespace FairPlay.Models;

/// <summary>
/// Equipo inscrito en UN torneo.   SQL: tabla  equipo.
/// Un equipo pertenece a un solo torneo (id_torneo); si el mismo club juega otro torneo,
/// se crea otro registro de equipo.
/// </summary>
[Table("equipo")]
public class Equipo
{
    /// <summary>SQL: id_equipo INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_equipo")]
    public int IdEquipo { get; set; }

    /// <summary>SQL: id_torneo INTEGER NOT NULL REFERENCES torneo ON DELETE CASCADE.</summary>
    [Column("id_torneo")]
    public int IdTorneo { get; set; }

    /// <summary>
    /// SQL: id_responsable INTEGER NOT NULL REFERENCES usuario ON DELETE RESTRICT.
    /// Usuario que inscribió el equipo. Lo asigna el servidor (usuario con sesión iniciada).
    /// </summary>
    [Column("id_responsable")]
    public int IdResponsable { get; set; }

    /// <summary>
    /// SQL: nombre TEXT NOT NULL COLLATE NOCASE CHECK (length(trim(nombre)) BETWEEN 2 AND 60);
    ///      UNIQUE (id_torneo, nombre)  -> no puede haber dos equipos con el mismo nombre en un
    ///      torneo (sin distinguir mayúsculas/minúsculas).
    /// </summary>
    [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
    [LongitudRecortada(2, 60)]
    [Display(Name = "Nombre del equipo")]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>SQL: ciudad TEXT NULL CHECK (ciudad IS NULL OR length(ciudad) &lt;= 60).</summary>
    [StringLength(60, ErrorMessage = "La ciudad no puede superar {1} caracteres.")]
    [Display(Name = "Ciudad")]
    [Column("ciudad")]
    public string? Ciudad { get; set; }

    /// <summary>SQL: logo_escudo TEXT NULL. Ruta relativa del archivo (ej. "escudos/halcones.png" en wwwroot).</summary>
    [Display(Name = "Escudo")]
    [Column("logo_escudo")]
    public string? LogoEscudo { get; set; }

    /// <summary>
    /// SQL: estado_inscripcion TEXT NOT NULL DEFAULT 'Pendiente' (Pendiente/Aprobado/Rechazado).
    /// Regla de la aplicación según torneo.modo_inscripcion:
    ///   Cerrada       -> solo el organizador inscribe.
    ///   ConAprobacion -> se crea Pendiente y el organizador decide.
    ///   Automatica    -> se crea Aprobado.
    /// </summary>
    [Display(Name = "Estado de inscripción")]
    [Column("estado_inscripcion")]
    public EstadoInscripcion EstadoInscripcion { get; set; } = EstadoInscripcion.Pendiente;

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Torneo en el que está inscrito (FK id_torneo).</summary>
    [ForeignKey(nameof(IdTorneo))]
    public Torneo? Torneo { get; set; }

    /// <summary>Usuario que lo inscribió (FK id_responsable).</summary>
    [ForeignKey(nameof(IdResponsable))]
    public Usuario? Responsable { get; set; }

    /// <summary>Plantel (jugador.id_equipo, ON DELETE CASCADE).</summary>
    public ICollection<Jugador> Jugadores { get; set; } = new List<Jugador>();

    /// <summary>Fases en las que participa (participante_fase.id_equipo, NO ACTION).</summary>
    public ICollection<ParticipanteFase> Participaciones { get; set; } = new List<ParticipanteFase>();

    /// <summary>Partidos donde juega de local (partido.id_local).</summary>
    public ICollection<Partido> PartidosComoLocal { get; set; } = new List<Partido>();

    /// <summary>Partidos donde juega de visitante (partido.id_visitante).</summary>
    public ICollection<Partido> PartidosComoVisitante { get; set; } = new List<Partido>();

    /// <summary>Eventos (goles, tarjetas) acreditados a este equipo (evento_partido.id_equipo).</summary>
    public ICollection<EventoPartido> Eventos { get; set; } = new List<EventoPartido>();
}
