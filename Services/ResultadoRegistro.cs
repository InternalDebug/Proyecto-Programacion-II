using FairPlay.Models;

namespace FairPlay.Services;

/// <summary>
/// Resultado de intentar registrar un usuario. En vez de lanzar excepciones por errores
/// "normales" (usuario repetido, correo repetido), el servicio devuelve este objeto y
/// el controlador decide qué mostrar.
/// </summary>
/// <param name="Usuario">El usuario creado; null si el registro falló.</param>
/// <param name="Campo">Propiedad del formulario a la que pertenece el error (vacío = error general).</param>
/// <param name="MensajeError">Mensaje para mostrar al usuario; null si todo salió bien.</param>
public record ResultadoRegistro(Usuario? Usuario, string? Campo, string? MensajeError)
{
    /// <summary>true si el usuario se creó correctamente.</summary>
    public bool Exito => Usuario is not null;

    public static ResultadoRegistro Correcto(Usuario usuario) => new(usuario, null, null);

    public static ResultadoRegistro Fallo(string campo, string mensaje) => new(null, campo, mensaje);
}
