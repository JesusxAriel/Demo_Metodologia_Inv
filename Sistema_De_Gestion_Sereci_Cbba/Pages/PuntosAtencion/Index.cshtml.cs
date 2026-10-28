using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;
using Sistema_De_Gestion_Sereci_Cbba.Services;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class PuntosAtencionModel(
    ApplicationDbContext context,
    IDynamicAssignmentServicio dynamicAssignmentServicio) : PageModel
{
    private static readonly string[] EstadosValidos =
        ["Activa", "Inactiva", "Finalizando turno", "En descanso"];

    public IReadOnlyList<PuntoAtencionDetalleDto> Puntos { get; private set; } = [];
    public IReadOnlyList<Tramite> Tramites { get; private set; } = [];
    public int PuntosActivos { get; private set; }
    public int PuntosInactivos { get; private set; }
    public int TurnosPendientes { get; private set; }
    public int PuntosNoDisponibles { get; private set; }

    [BindProperty]
    public PuntoAtencionFormulario Formulario { get; set; } = new();

    [TempData]
    public string? Mensaje { get; set; }

    public async Task OnGetAsync()
    {
        await CargarDatosAsync();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
    {
        ValidarFormulario();
        var punto = Formulario.Id > 0
            ? await context.PuntosAtencion
                .SingleOrDefaultAsync(item => item.Id == Formulario.Id)
            : null;

        if (Formulario.Id > 0 && punto is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await CargarDatosAsync();
            return Page();
        }

        var tramiteIds = Formulario.TramiteIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var tramitesValidos = await context.Tramites
            .Where(tramite => tramite.Estado == "Activo" &&
                              tramiteIds.Contains(tramite.Id))
            .Select(tramite => tramite.Id)
            .ToListAsync();

        if (tramitesValidos.Count != tramiteIds.Count)
        {
            ModelState.AddModelError(
                "Formulario.TramiteIds",
                "Uno o más trámites seleccionados no están activos.");
            await CargarDatosAsync();
            return Page();
        }

        var esNuevo = punto is null;
        var estadoAnterior = punto?.Estado;
        punto ??= new PuntoAtencion();
        punto.Nombre = Formulario.Nombre.Trim();
        punto.Estado = Formulario.Estado;
        if (esNuevo)
        {
            await context.PuntosAtencion.AddAsync(punto);
        }

        try
        {
            await using var transaction = await context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);
            if (esNuevo)
            {
                await context.SaveChangesAsync();
            }
            var relacionesActuales = await context.TramitesPuntosAtencion
                .Where(relation => relation.PuntoAtencionId == punto.Id)
                .ToListAsync();
            var idsActuales = relacionesActuales
                .Select(relation => relation.TramiteId)
                .ToHashSet();

            context.TramitesPuntosAtencion.RemoveRange(
                relacionesActuales.Where(relation =>
                    !tramiteIds.Contains(relation.TramiteId)));
            ReasignacionResultadoDto? reasignacion = null;
            if (!esNuevo && estadoAnterior != "Inactiva" &&
                punto.Estado == "Inactiva")
            {
                await context.SaveChangesAsync();
                reasignacion = await dynamicAssignmentServicio
                    .ReasignarTurnosPendientesAsync(
                        punto.Id,
                        "Desactivación desde la configuración de la ventanilla.");
            }

            await context.TramitesPuntosAtencion.AddRangeAsync(
                tramiteIds
                    .Where(id => !idsActuales.Contains(id))
                    .Select(id => new TramitePuntoAtencion
                    {
                        TramiteId = id,
                        PuntoAtencionId = punto.Id
                    }));
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            Mensaje = esNuevo
                ? "La ventanilla se creó correctamente."
                : "La ventanilla se actualizó correctamente.";
            if (reasignacion is not null)
            {
                Mensaje += $" Reasignados: {reasignacion.Reasignados}.";
                if (reasignacion.Bloqueados.Count > 0)
                {
                    Mensaje +=
                        $" Sin destino compatible: {string.Join(", ", reasignacion.Bloqueados)}.";
                }
            }
            return RedirectToPage();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                "Formulario.Nombre",
                "No fue posible guardar la ventanilla. Verifique que el nombre no esté repetido.");
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, string estado)
    {
        if (id <= 0 || !EstadosValidos.Contains(estado, StringComparer.Ordinal))
        {
            return BadRequest("El estado solicitado no es válido.");
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);
        var punto = await context.PuntosAtencion
            .SingleOrDefaultAsync(item => item.Id == id);
        if (punto is null)
        {
            return NotFound();
        }

        punto.Estado = estado;
        ReasignacionResultadoDto? reasignacion = null;
        if (estado == "Inactiva")
        {
            await context.SaveChangesAsync();
            reasignacion = await dynamicAssignmentServicio
                .ReasignarTurnosPendientesAsync(
                    id,
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
        return RedirectToPage();
    }

    private async Task CargarDatosAsync()
    {
        Tramites = await context.Tramites
            .AsNoTracking()
            .Where(tramite => tramite.Estado == "Activo")
            .OrderBy(tramite => tramite.Nombre)
            .ToListAsync();

        var puntos = await context.PuntosAtencion
            .AsNoTracking()
            .OrderBy(punto => punto.Id)
            .Select(punto => new { punto.Id, punto.Nombre, punto.Estado })
            .ToListAsync();

        var relaciones = await context.TramitesPuntosAtencion
            .AsNoTracking()
            .Where(relation => relation.Tramite.Estado == "Activo")
            .Select(relation => new
            {
                relation.PuntoAtencionId,
                TramiteId = relation.TramiteId,
                TramiteNombre = relation.Tramite.Nombre
            })
            .ToListAsync();

        var turnos = await context.TurnosSimulados
            .AsNoTracking()
            .Where(turno => turno.PuntoAtencionId.HasValue)
            .Select(turno => new
            {
                turno.Id,
                turno.CodigoTurno,
                turno.FechaHoraSolicitud,
                PuntoAtencionId = turno.PuntoAtencionId!.Value,
                turno.TramiteId,
                TramiteNombre = turno.Tramite.Nombre,
                turno.Estado
            })
            .ToListAsync();

        var estadosServicio = (await dynamicAssignmentServicio
                .ObtenerEstadosVentanillasAsync())
            .ToDictionary(item => item.Id);

        var pendientes = turnos
            .Where(turno => turno.Estado is "En espera" or "Asignado")
            .GroupBy(turno => (turno.PuntoAtencionId, turno.TramiteId))
            .ToDictionary(
                grupo => grupo.Key,
                grupo => new PuntoAtencionTramiteDto(
                    grupo.Key.TramiteId,
                    grupo.First().TramiteNombre,
                    grupo.Count()));

        var turnosPorPunto = turnos
            .Where(turno => turno.Estado is "En espera" or "Asignado")
            .GroupBy(turno => turno.PuntoAtencionId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo
                    .OrderBy(turno => turno.FechaHoraSolicitud)
                    .ThenBy(turno => turno.Id)
                    .Select((turno, indice) => new PuntoAtencionTurnoDto(
                        turno.Id,
                        indice + 1,
                        turno.CodigoTurno,
                        turno.TramiteNombre,
                        turno.FechaHoraSolicitud))
                    .ToList());

        var enAtencion = turnos
            .Where(turno => turno.Estado == "En atencion")
            .GroupBy(turno => turno.PuntoAtencionId)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

        Puntos = puntos
            .Select(punto =>
            {
                estadosServicio.TryGetValue(punto.Id, out var estadoServicio);
                var cola = pendientes
                    .Where(item => item.Key.PuntoAtencionId == punto.Id)
                    .Select(item => item.Value)
                    .OrderBy(item => item.Nombre)
                    .ToList();
                var compatibles = relaciones
                    .Where(relation => relation.PuntoAtencionId == punto.Id)
                    .Select(relation => new PuntoAtencionTramiteDto(
                        relation.TramiteId,
                        relation.TramiteNombre,
                        0))
                    .OrderBy(item => item.Nombre)
                    .ToList();
                var turnosCola = turnosPorPunto.GetValueOrDefault(punto.Id) ?? [];

                return new PuntoAtencionDetalleDto(
                    punto.Id,
                    punto.Nombre,
                    punto.Estado,
                    estadoServicio?.PuedeRecibirAsignaciones ?? false,
                    cola.Sum(item => item.TurnosPendientes),
                    cola.Count,
                    enAtencion.GetValueOrDefault(punto.Id),
                    compatibles,
                    cola,
                    turnosCola);
            })
            .ToList();

        PuntosActivos = puntos.Count(punto => punto.Estado == "Activa");
        PuntosInactivos = puntos.Count(punto => punto.Estado == "Inactiva");
        PuntosNoDisponibles = puntos.Count(punto =>
            punto.Estado is "Finalizando turno" or "En descanso");
        TurnosPendientes = Puntos.Sum(punto => punto.TurnosPendientes);
    }

    private void ValidarFormulario()
    {
        if (string.IsNullOrWhiteSpace(Formulario.Nombre) ||
            Formulario.Nombre.Trim().Length > 50)
        {
            ModelState.AddModelError(
                "Formulario.Nombre",
                "El nombre es obligatorio y no puede superar 50 caracteres.");
        }

        if (!EstadosValidos.Contains(Formulario.Estado, StringComparer.Ordinal))
        {
            ModelState.AddModelError(
                "Formulario.Estado",
                "Seleccione un estado operativo válido.");
        }
    }

    public sealed class PuntoAtencionFormulario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Estado { get; set; } = "Activa";
        public List<int> TramiteIds { get; set; } = [];
    }
}
