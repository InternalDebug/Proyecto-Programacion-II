using System.ComponentModel.DataAnnotations;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos de la pantalla "Buscar torneo por código".
/// A propósito NO lleva validaciones de largo ni de formato: el servicio ya recorta espacios y
/// convierte a mayúsculas, y un código incorrecto simplemente responde "no se encontró".
/// </summary>
public class BuscarTorneoViewModel
{
    [Display(Name = "Código del torneo")]
    public string? Codigo { get; set; }
}
