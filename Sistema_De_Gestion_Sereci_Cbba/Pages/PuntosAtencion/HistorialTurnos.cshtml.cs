using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class HistorialTurnosModel(ApplicationDbContext context) : PageModel
{
    public IReadOnlyList<TurnoHistorialDto> Turnos { get; private set; } = [];
    public IReadOnlyList<Tramite> Tramites { get; private set; } = [];
    public IReadOnlyList<PuntoAtencion> Puntos { get; private set; } = [];
    public int PaginaActual { get; private set; } = 1;
    public int TotalPaginas { get; private set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Desde { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Hasta { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? TramiteId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PuntoAtencionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Codigo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    public async Task OnGetAsync()
    {
        Tramites = await context.Tramites
            .AsNoTracking()
            .OrderBy(item => item.Nombre)
            .ToListAsync();
        Puntos = await context.PuntosAtencion
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync();

        var query = context.TurnosSimulados
            .AsNoTracking()
            .Include(turno => turno.Tramite)
            .Include(turno => turno.PuntoAtencion)
            .AsQueryable();

        if (Desde.HasValue)
        {
            query = query.Where(turno => turno.FechaHoraSolicitud >= Desde.Value.Date);
        }
        if (Hasta.HasValue)
        {
            query = query.Where(turno =>
                turno.FechaHoraSolicitud < Hasta.Value.Date.AddDays(1));
        }
        if (TramiteId.HasValue)
        {
            query = query.Where(turno => turno.TramiteId == TramiteId.Value);
        }
        if (PuntoAtencionId.HasValue)
        {
            query = query.Where(turno => turno.PuntoAtencionId == PuntoAtencionId.Value);
        }
        if (!string.IsNullOrWhiteSpace(Estado))
        {
            query = query.Where(turno => turno.Estado == Estado);
        }
        if (!string.IsNullOrWhiteSpace(Codigo))
        {
            var codigo = Codigo.Trim();
            query = query.Where(turno => turno.CodigoTurno.Contains(codigo));
        }

        var total = await query.CountAsync();
        const int tamanoPagina = 20;
        TotalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamanoPagina));
        PaginaActual = Math.Clamp(Pagina, 1, TotalPaginas);

        var turnos = await query
            .OrderBy(turno => turno.FechaHoraSolicitud)
            .ThenBy(turno => turno.Id)
            .Skip((PaginaActual - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync();
        var ids = turnos.Select(turno => turno.Id).ToList();
        var movimientos = await context.TurnosAsignacionesHistorial
            .AsNoTracking()
            .Include(item => item.PuntoAtencionOrigen)
            .Include(item => item.PuntoAtencionDestino)
            .Where(item => ids.Contains(item.TurnoSimuladoId))
            .OrderBy(item => item.FechaHoraMovimiento)
            .ThenBy(item => item.Id)
            .ToListAsync();

        Turnos = turnos.Select(turno =>
        {
            var historial = movimientos
                .Where(item => item.TurnoSimuladoId == turno.Id)
                .OrderBy(item => item.FechaHoraMovimiento)
                .ThenBy(item => item.Id)
                .ToList();
            var inicial = historial.FirstOrDefault(item =>
                item.TipoMovimiento == "Asignacion inicial");
            var ultimaReasignacion = historial.LastOrDefault(item =>
                item.TipoMovimiento == "Reasignacion");
            return new TurnoHistorialDto(
                turno.Id,
                turno.CodigoTurno,
                turno.Tramite.Nombre,
                turno.FechaHoraSolicitud,
                inicial?.PuntoAtencionDestino?.Nombre,
                inicial?.FechaHoraMovimiento,
                turno.PuntoAtencion?.Nombre,
                ultimaReasignacion?.FechaHoraMovimiento,
                turno.Estado,
                (turno.Estado == "En atencion" ||
                 turno.Estado == "Atendido")
                    ? turno.FechaHoraAtencion
                    : null,
                null,
                historial.Select(item => new TurnoMovimientoDto(
                    item.TipoMovimiento,
                    item.PuntoAtencionOrigen?.Nombre,
                    item.PuntoAtencionDestino?.Nombre,
                    item.FechaHoraMovimiento,
                    item.Motivo)).ToList());
        }).ToList();
    }
}
