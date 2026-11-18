namespace FairPlay.Models;

/// <summary>
/// Modelo de la página de error (Views/Shared/Error.cshtml). Viene de la plantilla de
/// ASP.NET Core MVC y NO tiene tabla en la base de datos: es solo un modelo de vista.
/// HomeController.Error() lo usa, por eso debe existir aunque Models/ ahora represente la BD.
/// </summary>
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
