namespace FairPlay.Services;

/// <summary>
/// Las cuatro formas en que puede terminar una operación del servicio.
/// El controlador traduce cada una a una respuesta HTTP distinta.
/// </summary>
public enum EstadoResultado
{
    /// <summary>Todo salió bien -> el controlador redirige o muestra la vista.</summary>
    Correcto,

    /// <summary>No existe un registro con ese código -> 404 (NotFound).</summary>
    NoEncontrado,

    /// <summary>Existe, pero el usuario no es el organizador -> 403 (Forbid).</summary>
    SinPermiso,

    /// <summary>Se rompió una regla de negocio -> se vuelve a mostrar el formulario con el mensaje.</summary>
    Invalido
}

/// <summary>
/// Resultado genérico de una operación del servicio (sirve para torneos y, más adelante,
/// para equipos, partidos, etc.). Evita lanzar excepciones por casos "normales".
/// </summary>
/// <typeparam name="T">Tipo del valor devuelto cuando todo sale bien (por ejemplo, Torneo).</typeparam>
public class ResultadoServicio<T> where T : class
{
    public EstadoResultado Estado { get; }

    /// <summary>El valor devuelto; solo tiene contenido cuando Estado es Correcto.</summary>
    public T? Valor { get; }

    /// <summary>Nombre de la propiedad del formulario con el problema (null o vacío = error general).</summary>
    public string? Campo { get; }

    /// <summary>Mensaje para mostrar al usuario cuando Estado es Invalido.</summary>
    public string? Mensaje { get; }

    public bool Exito => Estado == EstadoResultado.Correcto;

    private ResultadoServicio(EstadoResultado estado, T? valor, string? campo, string? mensaje)
    {
        Estado = estado;
        Valor = valor;
        Campo = campo;
        Mensaje = mensaje;
    }

    public static ResultadoServicio<T> Correcto(T valor) => new(EstadoResultado.Correcto, valor, null, null);

    public static ResultadoServicio<T> NoEncontrado() => new(EstadoResultado.NoEncontrado, null, null, null);

    public static ResultadoServicio<T> SinPermiso() => new(EstadoResultado.SinPermiso, null, null, null);

    public static ResultadoServicio<T> Invalido(string? campo, string mensaje) =>
        new(EstadoResultado.Invalido, null, campo, mensaje);
}
