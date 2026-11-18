using System.ComponentModel.DataAnnotations;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos del formulario de REGISTRO.
///
/// ¿Por qué un ViewModel y no la entidad Usuario directamente?
///  1) El formulario necesita "Confirmar contraseña", que no existe en la tabla usuario.
///  2) Usuario.PasswordHash NUNCA debe llegar desde el formulario (si no, un atacante podría
///     enviar su propio hash en la petición: es el ataque de "sobre-publicación" u overposting).
///  3) Así no hace falta el ModelState.Remove("PasswordHash") en este controlador.
///
/// Las reglas de NombreUsuario y Email son las MISMAS que las de Models/Usuario.cs,
/// que a su vez copian los CHECK del script SQL.
/// </summary>
public class RegistroViewModel
{
    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9_.-]+$", ErrorMessage = "El nombre de usuario solo puede tener letras, números, '_', '.' y '-'.")]
    [Display(Name = "Nombre de usuario")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(254, ErrorMessage = "El correo no puede superar {1} caracteres.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [RegularExpression(@"^.+@.+\..+$", ErrorMessage = "El correo debe tener el formato usuario@dominio.ext.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "La contraseña debe tener al menos una letra y un número.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debes confirmar la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarPassword { get; set; } = string.Empty;
}
