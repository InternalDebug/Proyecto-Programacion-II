using FairPlay.Data;
using FairPlay.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Services;

public class ServicioCuentas : IServicioCuentas
{
    private readonly FairPlayContext _contexto;
    private readonly IPasswordHasher<Usuario> _hasher;
    private readonly ILogger<ServicioCuentas> _logger;

    // Los tres objetos los entrega ASP.NET Core automáticamente (inyección de dependencias),
    // porque en Program.cs registramos el contexto, el hasher y este servicio.
    public ServicioCuentas(
        FairPlayContext contexto,
        IPasswordHasher<Usuario> hasher,
        ILogger<ServicioCuentas> logger)
    {
        _contexto = contexto;
        _hasher = hasher;
        _logger = logger;
    }

    // Set<Usuario>() funciona sin importar cómo se llame el DbSet dentro de FairPlayContext.
    private DbSet<Usuario> Usuarios => _contexto.Set<Usuario>();

    public async Task<ResultadoRegistro> RegistrarAsync(string nombreUsuario, string email, string password)
    {
        // El formulario no recorta espacios; lo hacemos aquí antes de guardar.
        nombreUsuario = nombreUsuario.Trim();
        email = email.Trim();

        // 1) Comprobamos antes de insertar para dar un mensaje claro.
        //    En SQLite las columnas nombre_usuario y email se comparan con COLLATE NOCASE
        //    (así lo define el script SQL), por lo que "Juan" y "juan" cuentan como el mismo.
        if (await Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario))
        {
            return ResultadoRegistro.Fallo(nameof(Usuario.NombreUsuario), "Ese nombre de usuario ya está en uso.");
        }

        if (await Usuarios.AnyAsync(u => u.Email == email))
        {
            return ResultadoRegistro.Fallo(nameof(Usuario.Email), "Ya existe una cuenta con ese correo.");
        }

        // 2) Creamos el usuario. La contraseña NUNCA se guarda: solo su hash
        //    (PBKDF2 con sal aleatoria, incluido en ASP.NET Core).
        var usuario = new Usuario
        {
            NombreUsuario = nombreUsuario,
            Email = email,
            FechaRegistro = DateTime.UtcNow
        };
        usuario.PasswordHash = _hasher.HashPassword(usuario, password);

        Usuarios.Add(usuario);

        // 3) Aunque ya comprobamos arriba, dos personas pueden registrarse a la vez con el
        //    mismo nombre: entonces el índice único de la base de datos rechaza el segundo
        //    INSERT y EF lanza DbUpdateException. Lo atrapamos para no mostrar una pantalla de error.
        try
        {
            await _contexto.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar al usuario {NombreUsuario}.", nombreUsuario);
            return ResultadoRegistro.Fallo(
                string.Empty,
                "No se pudo completar el registro. Es posible que el usuario o el correo ya existan; inténtalo de nuevo.");
        }

        return ResultadoRegistro.Correcto(usuario);
    }

    public async Task<Usuario?> ValidarCredencialesAsync(string usuarioOCorreo, string password)
    {
        var valor = usuarioOCorreo.Trim();

        // Un nombre de usuario no puede contener '@' (lo prohíbe la expresión regular del modelo),
        // así que un valor nunca puede coincidir con el nombre de un usuario y con el correo de otro.
        var usuario = await Usuarios
            .AsNoTracking() // solo lectura: EF no necesita "seguir" este objeto
            .FirstOrDefaultAsync(u => u.NombreUsuario == valor || u.Email == valor);

        if (usuario is null)
        {
            return null;
        }

        var resultado = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password);

        // Failed = contraseña incorrecta. Success y SuccessRehashNeeded = contraseña correcta
        // (el segundo significa que el algoritmo de hash fue actualizado; lo aceptamos igual).
        return resultado == PasswordVerificationResult.Failed ? null : usuario;
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int idUsuario)
    {
        return await Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
    }
}
