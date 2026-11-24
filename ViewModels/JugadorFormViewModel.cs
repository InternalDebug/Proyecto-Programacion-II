using System.ComponentModel.DataAnnotations;
using FairPlay.Models;
using FairPlay.Models.Enums;
using FairPlay.Models.Validaciones;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos del formulario de AGREGAR y EDITAR jugador. También se usa como "primer jugador" dentro
/// del formulario de inscripción de un equipo (ver InscribirEquipoViewModel).
///
/// Reglas copiadas de Models/Jugador.cs, que a su vez copian los CHECK del SQL:
///   OBLIGATORIOS: nombre (1-60), apellido (1-60), fecha de nacimiento, posición, dorsal (1-99).
///   OPCIONAL: la casilla de capitán (sin marcar = false).
/// El dorsal repetido dentro del equipo y el capitán único los comprueba el servicio (necesitan la BD).
/// </summary>
public class JugadorFormViewModel : IValidatableObject
{
    // --- Solo para la pantalla (vienen de la URL; el controlador NO confía en ellos) ---
    public string? CodigoTorneo { get; set; }
    public int IdEquipo { get; set; }
    public int IdJugador { get; set; }
    public string? NombreEquipo { get; set; }

    // --- Datos del jugador ---
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [LongitudRecortada(1, 60)]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [LongitudRecortada(1, 60)]
    [Display(Name = "Apellido")]
    public string Apellido { get; set; } = string.Empty;

    // DateOnly? + [Required]: así un campo vacío da NUESTRO mensaje en vez de uno genérico.
    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    public DateOnly? FechaNacimiento { get; set; }

    // [EnumDataType] rechaza números inventados (por ejemplo 99), que MVC enlazaría sin quejarse.
    [EnumDataType(typeof(PosicionJugador), ErrorMessage = "Elige una posición válida.")]
    [Display(Name = "Posición")]
    public PosicionJugador Posicion { get; set; }

    [Required(ErrorMessage = "El dorsal es obligatorio.")]
    [Range(1, 99, ErrorMessage = "El dorsal debe estar entre {1} y {2}.")]
    [Display(Name = "Dorsal")]
    public int? Dorsal { get; set; }

    [Display(Name = "¿Es capitán?")]
    public bool EsCapitan { get; set; }

    /// <summary>La fecha de nacimiento no puede ser futura ni absurdamente antigua.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaNacimiento.HasValue)
        {
            if (FechaNacimiento.Value > DateOnly.FromDateTime(DateTime.Today))
            {
                yield return new ValidationResult(
                    "La fecha de nacimiento no puede ser futura.",
                    new[] { nameof(FechaNacimiento) });
            }
            else if (FechaNacimiento.Value.Year < 1900)
            {
                yield return new ValidationResult(
                    "La fecha de nacimiento no es válida.",
                    new[] { nameof(FechaNacimiento) });
            }
        }
    }

    /// <summary>Rellena el formulario de edición con los datos actuales del jugador.</summary>
    public static JugadorFormViewModel DesdeJugador(Jugador jugador) => new()
    {
        IdJugador = jugador.IdJugador,
        IdEquipo = jugador.IdEquipo,
        Nombre = jugador.Nombre,
        Apellido = jugador.Apellido,
        FechaNacimiento = jugador.FechaNacimiento,
        Posicion = jugador.Posicion,
        Dorsal = jugador.Dorsal,
        EsCapitan = jugador.EsCapitan
    };
}
