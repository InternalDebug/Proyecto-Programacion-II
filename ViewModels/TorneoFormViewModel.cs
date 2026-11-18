using System.ComponentModel.DataAnnotations;
using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.Models.Validaciones;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos del formulario de CREAR y EDITAR torneo.
///
/// Contiene SOLO lo que el organizador puede escribir. No incluye IdTorneo, IdOrganizador,
/// Codigo ni Estado: los asigna el servidor. Así nadie puede cambiarlos enviando campos
/// extra en la petición (ataque de "sobre-publicación").
///
/// Por eso aquí NO hace falta el ModelState.Remove(nameof(Torneo.Codigo)) de las notas:
/// ese truco solo es necesario cuando el formulario se enlaza directamente a la entidad Torneo.
///
/// Las reglas son las mismas que Models/Torneo.cs, que a su vez copian los CHECK del SQL.
/// Los campos que el SQL permite NULL (descripción, máximo de equipos, fechas) son opcionales.
/// </summary>
public class TorneoFormViewModel : IValidatableObject
{
    /// <summary>
    /// Código del torneo. Solo se usa al EDITAR, para que el formulario sepa a qué dirección enviarse.
    /// El controlador NUNCA confía en este valor: usa el código de la URL.
    /// </summary>
    public string? Codigo { get; set; }

    [Required(ErrorMessage = "El nombre del torneo es obligatorio.")]
    [LongitudRecortada(3, 100)]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    // Opcional (la columna descripcion permite NULL). Un campo vacío llega como null.
    [StringLength(500, ErrorMessage = "La descripción no puede superar {1} caracteres.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    // [EnumDataType] rechaza números inventados (por ejemplo 99), que MVC enlazaría sin quejarse.
    [EnumDataType(typeof(FormatoTorneo), ErrorMessage = "Elige un formato válido.")]
    [Display(Name = "Formato")]
    public FormatoTorneo Formato { get; set; } = FormatoTorneo.Liga;

    [EnumDataType(typeof(ModoInscripcion), ErrorMessage = "Elige un modo de inscripción válido.")]
    [Display(Name = "Modo de inscripción")]
    public ModoInscripcion ModoInscripcion { get; set; } = ModoInscripcion.Cerrada;

    // Opcional (max_equipos permite NULL = sin límite). Si se escribe, mínimo 2.
    [Range(2, int.MaxValue, ErrorMessage = "El máximo de equipos debe ser al menos {1}.")]
    [Display(Name = "Máximo de equipos")]
    public int? MaximoEquipos { get; set; }

    // Los tres puntajes son NOT NULL en la BD; por eso son obligatorios y vienen con su valor por defecto.
    [Required(ErrorMessage = "Los puntos por victoria son obligatorios.")]
    [Range(0, int.MaxValue, ErrorMessage = "Los puntos por victoria no pueden ser negativos.")]
    [Display(Name = "Puntos por victoria")]
    public int? PuntosVictoria { get; set; } = 3;

    [Required(ErrorMessage = "Los puntos por empate son obligatorios.")]
    [Range(0, int.MaxValue, ErrorMessage = "Los puntos por empate no pueden ser negativos.")]
    [Display(Name = "Puntos por empate")]
    public int? PuntosEmpate { get; set; } = 1;

    [Required(ErrorMessage = "Los puntos por derrota son obligatorios.")]
    [Range(0, int.MaxValue, ErrorMessage = "Los puntos por derrota no pueden ser negativos.")]
    [Display(Name = "Puntos por derrota")]
    public int? PuntosDerrota { get; set; } = 0;

    // Opcionales (fecha_inicio y fecha_fin permiten NULL).
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de inicio")]
    public DateOnly? FechaInicio { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de fin")]
    public DateOnly? FechaFin { get; set; }

    /// <summary>Misma regla que el CHECK del SQL: la fecha de fin no puede ser anterior a la de inicio.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaInicio.HasValue && FechaFin.HasValue && FechaFin.Value < FechaInicio.Value)
        {
            yield return new ValidationResult(
                "La fecha de fin no puede ser anterior a la fecha de inicio.",
                new[] { nameof(FechaFin) });
        }
    }

    /// <summary>Rellena el formulario de edición con los datos actuales del torneo.</summary>
    public static TorneoFormViewModel DesdeTorneo(Torneo torneo) => new()
    {
        Codigo = torneo.Codigo,
        Nombre = torneo.Nombre,
        Descripcion = torneo.Descripcion,
        Formato = torneo.Formato,
        ModoInscripcion = torneo.ModoInscripcion,
        MaximoEquipos = torneo.MaxEquipos,
        PuntosVictoria = torneo.PuntosVictoria,
        PuntosEmpate = torneo.PuntosEmpate,
        PuntosDerrota = torneo.PuntosDerrota,
        FechaInicio = torneo.FechaInicio,
        FechaFin = torneo.FechaFin
    };
}
