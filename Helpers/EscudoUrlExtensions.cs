using Microsoft.AspNetCore.Mvc;

// Namespace raíz (FairPlay) a propósito: así las vistas ven este método sin agregar ningún @using,
// porque Views/_ViewImports.cshtml ya importa "FairPlay".
namespace FairPlay;

/// <summary>
/// Atajo para las vistas: devuelve la dirección de la imagen del escudo de un equipo.
/// Si el equipo no tiene escudo, devuelve el icono por defecto (wwwroot/img/escudo-default.svg).
///
/// Uso en una vista:   &lt;img src="@Url.UrlEscudo(equipo.LogoEscudo)" ... /&gt;
/// </summary>
public static class EscudoUrlExtensions
{
    /// <summary>Icono que se muestra cuando el equipo no subió escudo.</summary>
    public const string EscudoPorDefecto = "~/img/escudo-default.svg";

    public static string UrlEscudo(this IUrlHelper url, string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            return url.Content(EscudoPorDefecto);
        }

        // La BD guarda "escudos/xxxx.png"; "~/" lo convierte en una dirección válida del sitio.
        return url.Content("~/" + rutaRelativa);
    }
}
