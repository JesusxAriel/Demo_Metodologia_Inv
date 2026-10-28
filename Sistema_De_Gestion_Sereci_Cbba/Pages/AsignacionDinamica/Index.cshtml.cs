using System.Security.Cryptography;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;
using Sistema_De_Gestion_Sereci_Cbba.Services;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class AsignacionDinamicaModel(
    ApplicationDbContext context,
    IDynamicAssignmentServicio dynamicAssignmentServicio) : PageModel
{
    public IReadOnlyList<Tramite> Tramites { get; private set; } = [];
    public IReadOnlyList<EstadoVentanillaDto> Ventanillas { get; private set; } = [];

    [BindProperty]
    public int TramiteId { get; set; }

    public ResultadoAsignacionDto? Resultado { get; private set; }

    [TempData]
    public string? Mensaje { get; set; }

    public async Task OnGetAsync()
    {
        await CargarTramitesAsync();
    }

    public async Task<IActionResult> OnPostCalcularAsync()
    {
        await CargarTramitesAsync();

        if (TramiteId <= 0)
        {
            return ResponderConError("Seleccione un trámite válido.");
        }

        try
        {
            Resultado = await dynamicAssignmentServicio
                .CalcularAsignacionAsync(TramiteId);

            if (EsSolicitudAjax())
            {
                return new JsonResult(new
                {
                    resultado = Resultado,
                    ventanillas = await dynamicAssignmentServicio
                        .ObtenerEstadosVentanillasAsync(TramiteId)
                });
            }

            return Page();
        }
        catch (KeyNotFoundException exception)
        {
            return ResponderConError(exception.Message);
        }
    }

    public async Task<IActionResult> OnPostConfirmarAsync(
        int tramiteId,
        int puntoAtencionId,
        int tiempoEsperaEstimadoMinutos)
    {
        if (tramiteId <= 0 || puntoAtencionId <= 0)
        {
            return await ResponderConfirmacionConErrorAsync(
                "La recomendación no es válida. Calcule una nueva asignación.");
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            _ = await dynamicAssignmentServicio.CalcularAsignacionAsync(tramiteId);
        }
        catch (KeyNotFoundException)
        {
            return await ResponderConfirmacionConErrorAsync(
                "El trámite ya no está disponible. Calcule una nueva recomendación.");
        }

        var estadosActuales = await dynamicAssignmentServicio
            .ObtenerEstadosVentanillasAsync(tramiteId);
        var ventanillaSeleccionada = estadosActuales.SingleOrDefault(item =>
            item.Id == puntoAtencionId &&
            item.EsCompatible &&
            item.PuedeRecibirAsignaciones);

        if (ventanillaSeleccionada is null)
        {
            return await ResponderConfirmacionConErrorAsync(
                "La ventanilla seleccionada ya no está disponible para este trámite.");
        }

        if (ventanillaSeleccionada.TiempoEsperaEstimadoMinutos !=
            tiempoEsperaEstimadoMinutos)
        {
            return await ResponderConfirmacionConErrorAsync(
                "La disponibilidad cambió. Calcule nuevamente la asignación.");
        }

        string codigoTurno;
        try
        {
            codigoTurno = await GenerarCodigoTurnoAsync();
        }
        catch (InvalidOperationException)
        {
            return await ResponderConfirmacionConErrorAsync(
                "No se pudo generar el código del turno. Intente nuevamente.");
        }

        var turno = new TurnoSimulado
        {
            CodigoTurno = codigoTurno,
            TramiteId = tramiteId,
            PuntoAtencionId = puntoAtencionId,
            EscenarioId = null,
            FechaHoraSolicitud = DateTime.Now,
            TiempoEsperaEstimadoMinutos =
                ventanillaSeleccionada.TiempoEsperaEstimadoMinutos,
            Estado = "Asignado"
        };

        try
        {
            await context.TurnosSimulados.AddAsync(turno);
            await context.SaveChangesAsync();
            await context.TurnosAsignacionesHistorial.AddAsync(
                new TurnoAsignacionHistorial
                {
                    TurnoSimuladoId = turno.Id,
                    PuntoAtencionDestinoId = puntoAtencionId,
                    FechaHoraMovimiento = DateTime.Now,
                    TipoMovimiento = "Asignacion inicial",
                    Motivo = "Asignación confirmada por el encargado."
                });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return await ResponderConfirmacionConErrorAsync(
                "No se pudo confirmar la asignación. Intente nuevamente.");
        }

        var respuesta = new
        {
            codigoTurno,
            tramiteNombre = await context.Tramites
                .Where(item => item.Id == tramiteId)
                .Select(item => item.Nombre)
                .SingleAsync(),
            ventanillaNombre = ventanillaSeleccionada.Nombre,
            tiempoEsperaEstimadoMinutos =
                ventanillaSeleccionada.TiempoEsperaEstimadoMinutos,
            ventanillas = await dynamicAssignmentServicio
                .ObtenerEstadosVentanillasAsync(tramiteId)
        };

        if (EsSolicitudAjax())
        {
            return new JsonResult(respuesta);
        }

        await CargarTramitesAsync();
        return Page();
    }

   
    public async Task<IActionResult> OnPostCambiarEstadoAsync(
        int puntoAtencionId,
        string estado)
    {
        const string activa = "Activa";
        const string finalizando = "Finalizando turno";
        const string descanso = "En descanso";

        if (puntoAtencionId <= 0 ||
            (estado != activa && estado != finalizando && estado != descanso))
        {
            return ResponderConError("El estado solicitado no es válido.");
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);
        var ventanilla = await context.PuntosAtencion
            .SingleOrDefaultAsync(item => item.Id == puntoAtencionId);

        if (ventanilla is null || ventanilla.Estado == "Inactiva")
        {
            return ResponderConError(
                "Solo se puede cambiar el estado de una ventanilla operativa.");
        }

        ventanilla.Estado = estado;
        ReasignacionResultadoDto? reasignacion = null;
        if (estado == "Inactiva")
        {
            await context.SaveChangesAsync();
            reasignacion = await dynamicAssignmentServicio
                .ReasignarTurnosPendientesAsync(
                    puntoAtencionId,
                    "Cierre de la ventanilla.");
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        var detalleBloqueos = reasignacion is { Bloqueados.Count: > 0 }
            ? $" Turnos sin destino compatible: {string.Join(", ", reasignacion.Bloqueados)}."
            : string.Empty;
        var detalleReasignados = reasignacion is not null
            ? $" Reasignados: {reasignacion.Reasignados}."
            : string.Empty;
        Mensaje = $"La ventanilla ahora está {estado.ToLowerInvariant()}." +
            detalleReasignados + detalleBloqueos;

        // Las solicitudes AJAX reciben los datos actualizados en JSON.
        if (EsSolicitudAjax())
        {
            return new JsonResult(new
            {
                ventanillas = await dynamicAssignmentServicio
                    .ObtenerEstadosVentanillasAsync(
                        TramiteId > 0 ? TramiteId : null)
            });
        }

        // Las solicitudes normales vuelven a la página principal.
        return RedirectToPage("/PuntosAtencion/Index");
    }


    private async Task CargarTramitesAsync()
    {
        Tramites = await context.Tramites
            .AsNoTracking()
            .Where(tramite => tramite.Estado == "Activo")
            .OrderBy(tramite => tramite.Nombre)
            .ToListAsync();

        Ventanillas = await dynamicAssignmentServicio
            .ObtenerEstadosVentanillasAsync();
    }

    private IActionResult ResponderConError(string mensaje)
    {
        if (EsSolicitudAjax())
        {
            return BadRequest(new { mensaje });
        }

        ModelState.AddModelError(nameof(TramiteId), mensaje);
        return Page();
    }

    private async Task<IActionResult> ResponderConfirmacionConErrorAsync(string mensaje)
    {
        if (EsSolicitudAjax())
        {
            return BadRequest(new { mensaje });
        }

        ModelState.AddModelError(string.Empty, mensaje);
        await CargarTramitesAsync();
        return Page();
    }

    private async Task<string> GenerarCodigoTurnoAsync()
    {
        for (var intento = 0; intento < 3; intento++)
        {
            var codigo = $"SGAFU-{RandomNumberGenerator.GetInt32(10000000, 100000000)}";
            var existe = await context.TurnosSimulados
                .AsNoTracking()
                .AnyAsync(turno => turno.CodigoTurno == codigo);

            if (!existe)
            {
                return codigo;
            }
        }

        throw new InvalidOperationException();
    }

    private bool EsSolicitudAjax() =>
        string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
}
