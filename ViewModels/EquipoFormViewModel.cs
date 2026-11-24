using System.ComponentModel.DataAnnotations;
using FairPlay.Models;
using FairPlay.Models.Validaciones;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos del formulario de EDITAR equipo (y base del formulario de inscripción).
///
/// Contiene SOLO lo que el responsable puede escribir. No incluye IdTorneo, IdResponsable ni
/// EstadoInscripcion: los asigna el servidor, así nadie puede aprobarse a sí mismo enviando
/// campos extra en la petición.
///
/// Reglas copiadas de Models/Equipo.cs, que a su vez copian los CHECK del SQL:
///   OBLIGATORIO: nombre (2-60 sin contar espacios de los extremos).
///   OPCIONALES: ciudad (máx. 60) y escudo (imagen; si no hay, se muestra un icono por defecto).
/// </summary>
public class EquipoFormViewModel
{
    // --- Solo para la pantalla (vienen de la URL; el controlador NO confía en ellos) ---
    public string? CodigoTorneo { get; set; }
    public int IdEquipo { get; set; }

    /// <summary>Ruta del escudo actual, solo para mostrarlo al editar. El servicio usa el valor de la BD, no este.</summary>
    public string? EscudoActual { get; set; }

    // --- Datos del equipo ---
    [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
    [LongitudRecortada(2, 60)]
    [Display(Name = "Nombre del equipo")]
    public string Nombre { get; set; } = string.Empty;

    // Opcional (la columna ciudad permite NULL). Un campo vacío llega como null.
    [StringLength(60, ErrorMessage = "La ciudad no puede superar {1} caracteres.")]
    [Display(Name = "Ciudad")]
    public string? Ciudad { get; set; }

    // Opcional. "IFormFile?" con el signo ? es importante: sin él, .NET lo trataría como obligatorio.
    [Display(Name = "Escudo")]
    public IFormFile? Escudo { get; set; }

    [Display(Name = "Quitar el escudo actual")]
    public bool QuitarEscudo { get; set; }

    /// <summary>Rellena el formulario de edición con los datos actuales del equipo.</summary>
    public static EquipoFormViewModel DesdeEquipo(Equipo equipo) => new()
    {
        CodigoTorneo = equipo.Torneo?.Codigo,
        IdEquipo = equipo.IdEquipo,
        EscudoActual = equipo.LogoEscudo,
        Nombre = equipo.Nombre,
        Ciudad = equipo.Ciudad
    };
}
