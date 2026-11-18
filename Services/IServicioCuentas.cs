using FairPlay.Models;

namespace FairPlay.Services;

/// <summary>
/// Lógica de negocio de las cuentas de usuario (registro y validación de credenciales).
/// El controlador solo llama a estos métodos; no toca la base de datos ni el hash.
/// </summary>
public interface IServicioCuentas
{
    /// <summary>
    /// Crea un usuario nuevo con la contraseña ya convertida en hash.
    /// Devuelve un Fallo si el nombre de usuario o el correo ya existen.
    /// </summary>
    Task<ResultadoRegistro> RegistrarAsync(string nombreUsuario, string email, string password);

    /// <summary>
    /// Busca por nombre de usuario o correo y comprueba la contraseña.
    /// Devuelve null si el usuario no existe O si la contraseña es incorrecta
    /// (a propósito no se distingue, para no revelar qué usuarios existen).
    /// </summary>
    Task<Usuario?> ValidarCredencialesAsync(string usuarioOCorreo, string password);

    /// <summary>Busca un usuario por su id (null si no existe).</summary>
    Task<Usuario?> ObtenerPorIdAsync(int idUsuario);
}
