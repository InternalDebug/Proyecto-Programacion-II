namespace FairPlay.Services;

/// <summary>
/// Guarda y borra las imágenes de escudo de los equipos en la carpeta wwwroot/escudos.
/// En la base de datos (equipo.logo_escudo) solo se guarda la RUTA RELATIVA, por ejemplo
/// "escudos/3f9a1c...png"; el archivo vive en el disco.
/// </summary>
public interface IAlmacenEscudos
{
    /// <summary>
    /// Valida (tamaño, extensión y contenido real) y guarda la imagen con un nombre generado.
    /// Devuelve la ruta relativa a wwwroot, o un mensaje de error para mostrar al usuario.
    /// </summary>
    Task<(string? Ruta, string? Error)> GuardarAsync(IFormFile archivo);

    /// <summary>Borra el archivo de esa ruta relativa. Si no existe o la ruta no es válida, no hace nada.</summary>
    void Eliminar(string? rutaRelativa);
}
