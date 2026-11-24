using FairPlay.Models.Enums;
using FairPlay.Seguridad;
using FairPlay.Services;
using FairPlay.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairPlay.Controllers;

/// <summary>
/// CRUD de torneos, búsqueda por código y apertura/cierre de inscripciones.
/// Controlador "delgado": valida el formulario, llama a los servicios y traduce el resultado a HTTP.
///
/// Rutas (el "id" de la URL es el CÓDIGO de 8 caracteres, NO el id numérico de la tabla):
///   GET  /Torneos                           -> Mis torneos (los que organizo)
///   GET  /Torneos/Buscar?codigo=XXXX        -> Buscar un torneo por su código
///   GET  /Torneos/Details/{código}          -> Ver un torneo con sus equipos (quien conozca el código)
///   GET/POST /Torneos/Create                -> Crear
///   GET/POST /Torneos/Edit/{código}         -> Editar (solo el organizador)
///   GET/POST /Torneos/Delete/{código}       -> Eliminar (solo el organizador)
///   GET  /Torneos/ConfirmarEstado/{código}  -> Pantalla informativa antes de abrir/cerrar inscripciones
///   POST /Torneos/CambiarEstado/{código}    -> Abrir/cerrar inscripciones (solo el organizador)
///
/// [Authorize] a nivel de clase = TODAS las acciones exigen sesión iniciada,
/// salvo las marcadas con [AllowAnonymous] (Buscar y Details).
/// </summary>
[Authorize]
public class TorneosController : ControladorBase
{
    private readonly IServicioTorneos _torneos;
    private readonly IServicioEquipos _equipos;

    public TorneosController(IServicioTorneos torneos, IServicioEquipos equipos)
    {
        _torneos = torneos;
        _equipos = equipos;
    }

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
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Buscar(string? codigo)
    {
        var modelo = new BuscarTorneoViewModel { Codigo = codigo };

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

        return RedirectToAction(nameof(Details), new { id = torneo.Codigo });
    }

    // ------------------------------------------------------------------
    // DETALLE
    // ------------------------------------------------------------------

    // GET: /Torneos/Details/ABCD2345
    // Es pública: conocer el código es la "llave" para ver el torneo.
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
        bool esOrganizador = idUsuario == torneo.IdOrganizador;

        // Equipos que ESTE usuario puede ver (los Aprobados para todos; los demás según su rol).
        var equipos = await _equipos.ListarVisiblesAsync(torneo.IdTorneo, idUsuario, esOrganizador);

        // ¿Puede inscribir un equipo ahora? Solo se pregunta si hay sesión iniciada.
        bool puedeInscribir = false;
        if (idUsuario is not null)
        {
            var inscripcion = await _equipos.ObtenerTorneoParaInscribirAsync(idUsuario.Value, torneo.Codigo);
            puedeInscribir = inscripcion.Exito;
        }

        // ¿El organizador pidió no ver más la pantalla informativa del cambio de estado?
        bool omitirAviso = false;
        if (esOrganizador)
        {
            omitirAviso = AvisoOmitido(idUsuario!.Value, ReglasTorneo.EstadoSiguiente(torneo.Estado));
        }

        var modelo = new TorneoDetalleViewModel
        {
            Torneo = torneo,
            EsOrganizador = esOrganizador,
            Equipos = equipos,
            PuedeInscribir = puedeInscribir,
            OmitirAvisoCambioEstado = omitirAviso
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
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        // El servicio asigna el organizador (el usuario actual) y genera el código único.
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
    // ABRIR / CERRAR INSCRIPCIONES (solo el organizador)
    // ------------------------------------------------------------------

    // GET: /Torneos/ConfirmarEstado/ABCD2345?nuevoEstado=Inscripcion
    // Pantalla informativa que se muestra ANTES de cambiar el estado. No cambia nada por sí sola.
    [HttpGet]
    public async Task<IActionResult> ConfirmarEstado(string? id, EstadoTorneo? nuevoEstado)
    {
        var resultado = await _torneos.ObtenerParaGestionAsync(IdUsuarioActual, id);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        var torneo = resultado.Valor!;

        // Solo se ofrece la transición que de verdad está disponible desde el estado actual.
        if (nuevoEstado is null || ReglasTorneo.EstadoSiguiente(torneo.Estado) != nuevoEstado)
        {
            TempData["Error"] = "Ese cambio de estado no está disponible para este torneo.";
            return RedirectToAction(nameof(Details), new { id = torneo.Codigo });
        }

        return View(new CambioEstadoViewModel
        {
            Codigo = torneo.Codigo,
            NombreTorneo = torneo.Nombre,
            Modo = torneo.ModoInscripcion,
            NuevoEstado = nuevoEstado.Value
        });
    }

    // POST: /Torneos/CambiarEstado/ABCD2345
    // Se llega desde la pantalla informativa (con o sin la casilla "No mostrar de nuevo")
    // o directamente desde el botón del detalle, si el organizador ya marcó esa casilla.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(string? id, EstadoTorneo? nuevoEstado, bool noMostrarMas)
    {
        // Si el formulario no trae el estado destino, la petición está mal formada.
        if (nuevoEstado is null)
        {
            return BadRequest();
        }

        var resultado = await _torneos.CambiarEstadoAsync(IdUsuarioActual, id, nuevoEstado.Value);

        var error = RespuestaDeError(resultado);
        if (error is not null)
        {
            return error;
        }

        if (!resultado.Exito) // Invalido: la transición no está permitida
        {
            TempData["Error"] = resultado.Mensaje;
            return RedirectToAction(nameof(Details), new { id });
        }

        // "No mostrar de nuevo": recordamos la elección en una cookie (solo si el cambio salió bien).
        if (noMostrarMas)
        {
            Response.Cookies.Append(NombreCookieAviso(IdUsuarioActual, nuevoEstado.Value), "1", new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1), // dura un año
                HttpOnly = true,                              // JavaScript no puede leerla
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Secure = Request.IsHttps
            });
        }

        TempData["Mensaje"] = nuevoEstado == EstadoTorneo.Inscripcion
            ? "Inscripciones abiertas: desde ahora se pueden inscribir equipos."
            : "Inscripciones cerradas: el torneo volvió a borrador.";

        return RedirectToAction(nameof(Details), new { id = resultado.Valor!.Codigo });
    }

    // ------------------------------------------------------------------
    // Métodos auxiliares privados (cookie "No mostrar de nuevo")
    // ------------------------------------------------------------------

    /// <summary>
    /// Nombre de la cookie que recuerda "No mostrar de nuevo". Lleva el estado destino y el id del
    /// usuario: así abrir y cerrar tienen su propia elección, y en un navegador compartido cada
    /// usuario conserva la suya. Es por navegador (no se guarda en la base de datos).
    /// </summary>
    private static string NombreCookieAviso(int idUsuario, EstadoTorneo destino) =>
        $"FairPlay.OmitirAviso.{destino}.{idUsuario}";

    private bool AvisoOmitido(int idUsuario, EstadoTorneo? destino) =>
        destino is not null && Request.Cookies[NombreCookieAviso(idUsuario, destino.Value)] == "1";
}
