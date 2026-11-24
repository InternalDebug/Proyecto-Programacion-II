namespace FairPlay.Services;

public class AlmacenEscudos : IAlmacenEscudos
{
    /// <summary>Carpeta (dentro de wwwroot) donde se guardan los escudos.</summary>
    private const string Carpeta = "escudos";

    /// <summary>Tamaño máximo permitido: 2 MB.</summary>
    private const long TamanoMaximoBytes = 2 * 1024 * 1024;

    private static readonly string[] ExtensionesPermitidas = { ".png", ".jpg", ".jpeg", ".webp" };

    private readonly IWebHostEnvironment _entorno;
    private readonly ILogger<AlmacenEscudos> _logger;

    public AlmacenEscudos(IWebHostEnvironment entorno, ILogger<AlmacenEscudos> logger)
    {
        _entorno = entorno;
        _logger = logger;
    }

    /// <summary>Ruta física de wwwroot (si por algún motivo no está definida, se arma desde la raíz del proyecto).</summary>
    private string RaizWeb => _entorno.WebRootPath ?? Path.Combine(_entorno.ContentRootPath, "wwwroot");

    public async Task<(string? Ruta, string? Error)> GuardarAsync(IFormFile archivo)
    {
        // 1) Tamaño
        if (archivo.Length == 0)
        {
            return (null, "El archivo del escudo está vacío.");
        }

        if (archivo.Length > TamanoMaximoBytes)
        {
            return (null, "El escudo no puede pesar más de 2 MB.");
        }

        // 2) Extensión permitida (la extensión la escribe el usuario: NO basta para confiar en el archivo)
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

        if (!ExtensionesPermitidas.Contains(extension))
        {
            return (null, "El escudo debe ser una imagen PNG, JPG o WEBP.");
        }

        // 3) Contenido real: leemos los primeros bytes y comprobamos que de verdad sea esa imagen.
        //    Así un archivo .exe o .html renombrado a .png se rechaza.
        var cabecera = new byte[12];
        await using (var lectura = archivo.OpenReadStream())
        {
            int leidos = await lectura.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false);

            if (leidos < cabecera.Length || !CoincideConFormato(extension, cabecera))
            {
                return (null, "El archivo no es una imagen válida (PNG, JPG o WEBP).");
            }
        }

        // 4) Guardamos con un nombre generado por nosotros: nunca usamos el nombre que trae el usuario
        //    (podría repetirse, tener caracteres raros o intentar escribir en otra carpeta).
        var carpetaFisica = Path.Combine(RaizWeb, Carpeta);
        Directory.CreateDirectory(carpetaFisica); // si la carpeta no existe, la crea

        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
        var destino = Path.Combine(carpetaFisica, nombreArchivo);

        await using (var salida = new FileStream(destino, FileMode.CreateNew))
        {
            await archivo.CopyToAsync(salida);
        }

        // Esta es la ruta que se guarda en la BD (siempre con "/", relativa a wwwroot).
        return ($"{Carpeta}/{nombreArchivo}", null);
    }

    public void Eliminar(string? rutaRelativa)
    {
        // Solo borramos archivos que estén dentro de wwwroot/escudos.
        if (string.IsNullOrWhiteSpace(rutaRelativa) || !rutaRelativa.StartsWith(Carpeta + "/", StringComparison.Ordinal))
        {
            return;
        }

        // GetFileName se queda solo con el nombre final: así un valor como "escudos/../../algo" no puede salirse de la carpeta.
        var completo = Path.Combine(RaizWeb, Carpeta, Path.GetFileName(rutaRelativa));

        try
        {
            if (File.Exists(completo))
            {
                File.Delete(completo);
            }
        }
        catch (IOException ex)
        {
            // Un escudo huérfano en el disco no es grave: lo registramos y seguimos.
            _logger.LogWarning(ex, "No se pudo borrar el escudo {Ruta}.", rutaRelativa);
        }
    }

    /// <summary>Compara los primeros bytes del archivo con la "firma" de cada formato de imagen.</summary>
    private static bool CoincideConFormato(string extension, byte[] c) => extension switch
    {
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        ".png" => c[0] == 0x89 && c[1] == 0x50 && c[2] == 0x4E && c[3] == 0x47
                  && c[4] == 0x0D && c[5] == 0x0A && c[6] == 0x1A && c[7] == 0x0A,

        // JPEG: FF D8 FF
        ".jpg" or ".jpeg" => c[0] == 0xFF && c[1] == 0xD8 && c[2] == 0xFF,

        // WEBP: "RIFF" + 4 bytes de tamaño + "WEBP"
        ".webp" => c[0] == 'R' && c[1] == 'I' && c[2] == 'F' && c[3] == 'F'
                   && c[8] == 'W' && c[9] == 'E' && c[10] == 'B' && c[11] == 'P',

        _ => false
    };
}
