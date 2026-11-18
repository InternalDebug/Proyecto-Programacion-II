using System.ComponentModel.DataAnnotations;

namespace FairPlay.ViewModels;

/// <summary>
/// Datos del formulario de INICIO DE SESIÓN.
/// Se puede entrar con el nombre de usuario o con el correo.
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Escribe tu nombre de usuario o tu correo.")]
    [Display(Name = "Usuario o correo")]
    public string UsuarioOCorreo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Mantener la sesión iniciada")]
    public bool Recordarme { get; set; }

    /// <summary>
    /// Página a la que volver después de iniciar sesión (la rellena ASP.NET cuando
    /// te redirige al login desde una página protegida con [Authorize]).
    /// Va en un campo oculto del formulario y SIEMPRE se valida con Url.IsLocalUrl.
    /// </summary>
    public string? ReturnUrl { get; set; }
}
