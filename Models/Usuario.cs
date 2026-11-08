using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FairPlay.Models;

[Table("usuario")]
public class Usuario
{

    [Key]
    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9_.-]+$", ErrorMessage = "El nombre de usuario solo puede tener letras, números, '_', '.' y '-'.")]
    [Display(Name = "Nombre de usuario")]
    [Column("nombre_usuario")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(254, ErrorMessage = "El correo no puede superar {1} caracteres.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [RegularExpression(@"^.+@.+\..+$", ErrorMessage = "El correo debe tener el formato usuario@dominio.ext.")]
    [Display(Name = "Correo electrónico")]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [DataType(DataType.DateTime)]
    [Display(Name = "Fecha de registro")]
    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // ------------------------------------------------------------------
    // Propiedades de navegación (no son columnas; EF las llena con Include)
    // ------------------------------------------------------------------

    /// <summary>Torneos que este usuario organiza (torneo.id_organizador, ON DELETE RESTRICT).</summary>
    public ICollection<Torneo> TorneosOrganizados { get; set; } = new List<Torneo>();

    /// <summary>Equipos que este usuario inscribió (equipo.id_responsable, ON DELETE RESTRICT).</summary>
    public ICollection<Equipo> EquiposResponsable { get; set; } = new List<Equipo>();

    /// <summary>Partidos que arbitra (partido.id_arbitro, ON DELETE SET NULL).</summary>
    public ICollection<Partido> PartidosArbitrados { get; set; } = new List<Partido>();

    /// <summary>Cambios de estado de partido hechos por este usuario (auditoría).</summary>
    public ICollection<CambioEstadoPartido> CambiosEstado { get; set; } = new List<CambioEstadoPartido>();

    /// <summary>Eventos (goles, tarjetas) que este usuario registró.</summary>
    public ICollection<EventoPartido> EventosRegistrados { get; set; } = new List<EventoPartido>();
}
