using FairPlay.Seguridad;
using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// CRUD de torneos y búsqueda por código. Controlador "delgado": valida el formulario,
/// llama a IServicioTorneos y traduce el resultado a una respuesta HTTP.
///
/// Rutas (el "id" de la URL es el CÓDIGO de 8 caracteres, NO el id numérico de la tabla):
///   GET  /Torneos                    -> Mis torneos (los que organizo)
///   GET  /Torneos/Buscar?codigo=XXXX -> Buscar un torneo por su código
///   GET  /Torneos/Details/{código}   -> Ver un torneo (cualquiera que conozca el código)
///   GET/POST /Torneos/Create         -> Crear
///   GET/POST /Torneos/Edit/{código}  -> Editar (solo el organizador)
///   GET/POST /Torneos/Delete/{código}-> Eliminar (solo el organizador)
///
/// [Authorize] a nivel de clase = TODAS las acciones exigen sesión iniciada,
/// salvo las marcadas con [AllowAnonymous] (Buscar y Details).
/// Es la opción segura por defecto: si en el futuro agregas una acción y olvidas protegerla,
/// ya queda protegida.
/// </summary>
[Authorize]
public class TorneosController : Controller
{
    private readonly IServicioTorneos _torneos;

    public TorneosController(IServicioTorneos torneos)
    {
        _torneos = torneos;
    }

    /// <summary>
    /// Id del usuario con sesión iniciada. Solo se usa en acciones con [Authorize], donde la cookie
    /// siempre trae el id (lo guarda CuentaController.IniciarSesionAsync).
    /// </summary>
    private int IdUsuarioActual =>
        User.ObtenerIdUsuario() ?? throw new InvalidOperationException("La sesión no contiene el id del usuario.");

    // ------------------------------------------------------------------
    // LISTA: MIS TORNEOS (opción B: cada usuario solo ve los que organiza)
    // ------------------------------------------------------------------

    // GET: /Torneos
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var misTorneos = await _torneos.ObtenerDeOrganizadorAsync(IdUsuarioActual);
        return View(misTorneos);
    }

    // ------------------------------------------------------------------
    // BUSCAR POR CÓDIGO
    // ------------------------------------------------------------------

    // GET: /Torneos/Buscar            -> formulario vacío
    // GET: /Torneos/Buscar?codigo=XXX -> busca y, si existe, redirige al detalle
    // Es GET (no POST) porque buscar no modifica nada y así el enlace se puede compartir.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Buscar(string? codigo)
    {
        var modelo = new BuscarTorneoViewModel { Codigo = codigo };

        // Primera visita (sin código escrito): solo mostramos el formulario.
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return View(modelo);
        }

        var torneo = await _torneos.ObtenerPorCodigoAsync(codigo);

        if (torneo is null)
        {
            // Mensaje genérico: no decimos si el código tenía mal el formato o simplemente no existe.
            ModelState.AddModelError(nameof(modelo.Codigo), "No se encontró ningún torneo con ese código.");
            return View(modelo);
        }

        // Redirigimos con el código ya normalizado (mayúsculas, sin espacios) tal como está guardado.
        return RedirectToAction(nameof(Details), new { id = torneo.Codigo });
    }

    // ------------------------------------------------------------------
    // DETALLE
    // ------------------------------------------------------------------

    // GET: /Torneos/Details/ABCD2345
    // Es pública: conocer el código es la "llave" para ver el torneo (por eso se usa el código
    // y no el id numérico, que se podría adivinar probando 1, 2, 3...).
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Details(string? id)
    {
        var torneo = await _torneos.ObtenerPorCodigoAsync(id);

        if (torneo is null)
        {
            return NotFound();
        }

        // Un visitante sin sesión devuelve null -> la comparación da false (no es organizador).
        var idUsuario = User.ObtenerIdUsuario();

        var modelo = new TorneoDetalleViewModel
        {
            Torneo = torneo,
            EsOrganizador = idUsuario == torneo.IdOrganizador
        };

        return View(modelo);
    }

    // ------------------------------------------------------------------
    // CREAR
    // ------------------------------------------------------------------

    // GET: /Torneos/Create -> formulario con los valores por defecto (3, 1, 0 puntos; Liga; Cerrada)
    [HttpGet]
    public IActionResult Create()
    {
        return View(new TorneoFormViewModel());
    }

    // POST: /Torneos/Create
    [HttpPost]
    [ValidateAntiForgeryToken] // rechaza formularios que no vengan de nuestra propia página (CSRF)
    public async Task<IActionResult> Create(TorneoFormViewModel modelo)
    {
        // 1) Validaciones de los atributos del ViewModel (largo, rangos, fechas...).
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // 2) El servicio asigna el organizador (el usuario actual) y genera el código único.
        //    El formulario no puede influir en ninguno de los dos.
        var resultado = await _torneos.CrearAsync(IdUsuarioActual, modelo);

        if (!resultado.Exito)
        {
            ModelState.AddModelError(resultado.Campo ?? string.Empty, resultado.Mensaje ?? "No se pudo crear el torneo.");
            return View(modelo);
        }

        var torneo = resultado.Valor!;

        TempData["Mensaje"] = $"Torneo creado. Su código es {torneo.Codigo}: compártelo para que otros puedan encontrarlo.";
        return RedirectToAction(nameof(Details), new { id = torneo.Codigo });
    }

    // ------------------------------------------------------------------
    // EDITAR (solo el organizador)
    // ------------------------------------------------------------------

    // GET: /Torneos/Edit/ABCD2345
    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        var resultado = await _torneos.ObtenerParaGestionAsync(IdUsuarioActual, id);

        // NoEncontrado -> 404 | SinPermiso -> 403 (ASP.NET redirige a /Cuenta/AccesoDenegado)
        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        return View(TorneoFormViewModel.DesdeTorneo(resultado.Valor!));
    }

    // POST: /Torneos/Edit/ABCD2345
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string? id, TorneoFormViewModel modelo)
    {
        // Pase lo que pase, el torneo a modificar es el de la URL. Se ignora el Codigo que traiga el formulario.
        modelo.Codigo = id;

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // El servicio vuelve a comprobar que el usuario sea el organizador:
        // alguien podría enviar este POST directamente, sin pasar por el GET.
        var resultado = await _torneos.EditarAsync(IdUsuarioActual, id, modelo);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: se rompió una regla de negocio
        {
            ModelState.AddModelError(resultado.Campo ?? string.Empty, resultado.Mensaje ?? "No se pudieron guardar los cambios.");
            return View(modelo);
        }

        TempData["Mensaje"] = "Los cambios del torneo se guardaron correctamente.";
        return RedirectToAction(nameof(Details), new { id = resultado.Valor!.Codigo });
    }

    // ------------------------------------------------------------------
    // ELIMINAR (solo el organizador)
    // ------------------------------------------------------------------

    // GET: /Torneos/Delete/ABCD2345 -> pantalla de confirmación
    [HttpGet]
    public async Task<IActionResult> Delete(string? id)
    {
        var resultado = await _torneos.ObtenerParaGestionAsync(IdUsuarioActual, id);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        return View(resultado.Valor);
    }

    // POST: /Torneos/Delete/ABCD2345
    // [ActionName("Delete")]: se llama DeleteConfirmed en C# (no se pueden repetir dos métodos
    // Delete con la misma firma), pero responde a la misma URL /Torneos/Delete. Es el patrón de CineMVC.
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string? id)
    {
        var resultado = await _torneos.EliminarAsync(IdUsuarioActual, id);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: la BD no permitió borrarlo
        {
            TempData["Error"] = resultado.Mensaje;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Mensaje"] = $"El torneo \"{resultado.Valor!.Nombre}\" se eliminó correctamente.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------
    // Método auxiliar privado
    // ------------------------------------------------------------------

    /// <summary>
    /// Traduce NoEncontrado y SinPermiso a su respuesta HTTP. Devuelve null cuando el resultado es
    /// Correcto o Invalido: esos dos casos los resuelve cada acción a su manera.
    /// Así no se repite el mismo if en las cuatro acciones que gestionan un torneo.
    /// </summary>
    private IActionResult? RespuestaDeError<T>(ResultadoServicio<T> resultado) where T : class
    {
        if (resultado.Estado == EstadoResultado.NoEncontrado)
        {
            return NotFound(); // 404: el código no existe
        }

        if (resultado.Estado == EstadoResultado.SinPermiso)
        {
            return Forbid(); // 403: existe, pero el usuario no es el organizador
        }

        return null;
    }
}
