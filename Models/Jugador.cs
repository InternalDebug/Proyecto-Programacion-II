using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FairPlay.Models.Enums;
using FairPlay.Models.Validaciones;

namespace FairPlay.Models;

/// <summary>
/// Jugador de un equipo.   SQL: tabla  jugador.
/// Posiciones y rango de dorsal según la guía de CRUD de jugadores.
/// </summary>
[Table("jugador")]
public class Jugador
{
    /// <summary>SQL: id_jugador INTEGER PRIMARY KEY AUTOINCREMENT.</summary>
    [Key]
    [Column("id_jugador")]
    public int IdJugador { get; set; }

    /// <summary>SQL: id_equipo INTEGER NOT NULL REFERENCES equipo ON DELETE CASCADE.</summary>
    [Column("id_equipo")]
    public int IdEquipo { get; set; }

    /// <summary>SQL: nombre TEXT NOT NULL CHECK (length(trim(nombre)) BETWEEN 1 AND 60).</summary>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [LongitudRecortada(1, 60)]
    [Display(Name = "Nombre")]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>SQL: apellido TEXT NOT NULL CHECK (length(trim(apellido)) BETWEEN 1 AND 60).</summary>
    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [LongitudRecortada(1, 60)]
    [Display(Name = "Apellido")]
    [Column("apellido")]
    public string Apellido { get; set; } = string.Empty;

    /// <summary>
    /// SQL: fecha_nacimiento TEXT NOT NULL
    ///      CHECK (fecha_nacimiento GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]').
    /// El CHECK exige EXACTAMENTE "yyyy-MM-dd" (sin hora). Por eso es DateOnly y no DateTime:
    /// un DateTime se guardaría como "2000-03-14 00:00:00" y la BD lo rechazaría.
    /// </summary>
    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    [Column("fecha_nacimiento")]
    public DateOnly FechaNacimiento { get; set; }

    /// <summary>SQL: posicion TEXT NOT NULL CHECK (IN ('Arquero','Defensa','Medio campo','Delantero')).</summary>
    [Display(Name = "Posición")]
    [Column("posicion")]
    public PosicionJugador Posicion { get; set; }

    /// <summary>SQL: dorsal INTEGER NOT NULL CHECK (dorsal BETWEEN 1 AND 99); UNIQUE (id_equipo, dorsal).</summary>
    [Range(1, 99, ErrorMessage = "El dorsal debe estar entre {1} y {2}.")]
    [Display(Name = "Dorsal")]
    [Column("dorsal")]
    public int Dorsal { get; set; }

    /// <summary>
    /// SQL: es_capitan INTEGER NOT NULL DEFAULT 0 CHECK (IN (0,1)).
    /// Índice único parcial uq_jugador_capitan: como máximo UN capitán por equipo. Al marcar un
    /// nuevo capitán, el controlador debe desmarcar antes al anterior.
    /// </summary>
    [Display(Name = "¿Es capitán?")]
    [Column("es_capitan")]
    public bool EsCapitan { get; set; }

    /// <summary>Nombre y apellido juntos (útil para listas desplegables). No es una columna.</summary>
    [NotMapped]
    public string NombreCompleto => $"{Nombre} {Apellido}";

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    /// <summary>Equipo al que pertenece (FK id_equipo).</summary>
    [ForeignKey(nameof(IdEquipo))]
    public Equipo? Equipo { get; set; }

    /// <summary>Eventos protagonizados por el jugador (evento_partido.id_jugador, NO ACTION).</summary>
    public ICollection<EventoPartido> Eventos { get; set; } = new List<EventoPartido>();
}
