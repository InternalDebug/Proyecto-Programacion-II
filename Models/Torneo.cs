using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using FairPlay.Models.Enums;
using FairPlay.Models.Validaciones;

namespace FairPlay.Models;

/// </summary>
[Table("torneo")]
public class Torneo : IValidatableObject
{
    /// <summary>Alfabeto del código: 32 símbolos, SIN I, O, 0 ni 1 (se confunden al leerlos).</summary>
    public const string AlfabetoCodigo = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Longitud del código de torneo.</summary>
    public const int LongitudCodigo = 8;
    
    [Key]
    [Column("id_torneo")]
    public int IdTorneo { get; set; }

    /// <summary>
    /// SQL: id_organizador INTEGER NOT NULL REFERENCES usuario ON DELETE RESTRICT.
    /// Lo asigna el servidor con el usuario que inició sesión (no viene del formulario).
    /// </summary>
    [Column("id_organizador")]
    public int IdOrganizador { get; set; }

    /// <summary>
    /// SQL: codigo TEXT NOT NULL UNIQUE
    ///      CHECK (8 caracteres del alfabeto [A-HJ-NP-Z2-9], en MAYÚSCULAS).
    ///
    /// Lo genera la aplicación (Torneo.GenerarCodigo()), NO el usuario. Por eso, en el
    /// controlador Create: asignar el código y luego ModelState.Remove(nameof(Torneo.Codigo)),
    /// porque el formulario no lo envía. Si el INSERT falla por UNIQUE (colisión), generar otro.
    /// </summary>
    [Required]
    [RegularExpression(@"^[A-HJ-NP-Z2-9]{8}$", ErrorMessage = "El código debe tener 8 caracteres (letras mayúsculas sin I ni O, y números del 2 al 9).")]
    [Display(Name = "Código del torneo")]
    [Column("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del torneo es obligatorio.")]
    [LongitudRecortada(3, 100)]
    [Display(Name = "Nombre")]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede superar {1} caracteres.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Descripción")]
    [Column("descripcion")]
    public string? Descripcion { get; set; }

    [Display(Name = "Formato")]
    [Column("formato")]
    public FormatoTorneo Formato { get; set; }

    [Display(Name = "Estado")]
    [Column("estado")]
    public EstadoTorneo Estado { get; set; } = EstadoTorneo.Borrador;

    [Display(Name = "Modo de inscripción")]
    [Column("modo_inscripcion")]
    public ModoInscripcion ModoInscripcion { get; set; } = ModoInscripcion.Cerrada;

    [Range(2, int.MaxValue, ErrorMessage = "El máximo de equipos debe ser al menos {1}.")]
    [Display(Name = "Máximo de equipos")]
    [Column("max_equipos")]
    public int? MaxEquipos { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Los puntos por victoria no pueden ser negativos.")]
    [Display(Name = "Puntos por victoria")]
    [Column("puntos_victoria")]
    public int PuntosVictoria { get; set; } = 3;

    [Range(0, int.MaxValue, ErrorMessage = "Los puntos por empate no pueden ser negativos.")]
    [Display(Name = "Puntos por empate")]
    [Column("puntos_empate")]
    public int PuntosEmpate { get; set; } = 1;

    [Range(0, int.MaxValue, ErrorMessage = "Los puntos por derrota no pueden ser negativos.")]
    [Display(Name = "Puntos por derrota")]
    [Column("puntos_derrota")]
    public int PuntosDerrota { get; set; } = 0;

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de inicio")]
    [Column("fecha_inicio")]
    public DateOnly? FechaInicio { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de fin")]
    [Column("fecha_fin")]
    public DateOnly? FechaFin { get; set; }

    // ------------------------------------------------------------------
    // Propiedades de navegación
    // ------------------------------------------------------------------

    [ForeignKey(nameof(IdOrganizador))]
    public Usuario? Organizador { get; set; }

    public ICollection<Fase> Fases { get; set; } = new List<Fase>();

    public ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();

    // ------------------------------------------------------------------
    // Reglas y utilidades
    // ------------------------------------------------------------------

    public static string GenerarCodigo()
    {
        var caracteres = new char[LongitudCodigo];
        for (int i = 0; i < caracteres.Length; i++)
        {
            caracteres[i] = AlfabetoCodigo[RandomNumberGenerator.GetInt32(AlfabetoCodigo.Length)];
        }
        return new string(caracteres);
    }
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaInicio.HasValue && FechaFin.HasValue && FechaFin.Value < FechaInicio.Value)
        {
            yield return new ValidationResult(
                "La fecha de fin no puede ser anterior a la fecha de inicio.",
                new[] { nameof(FechaFin) });
        }
    }
}
