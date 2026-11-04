using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;
using Sistema_De_Gestion_Sereci_Cbba.Services;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class IndexModel(
    ApplicationDbContext context,
    IDynamicAssignmentServicio dynamicAssignmentServicio) : PageModel
{
    public int DemandaTotal { get; private set; }

    public int PuntosActivos { get; private set; }

    public int TramitesActivos { get; private set; }

    public int TurnosPendientes { get; private set; }

    public string FranjaMayorDemanda { get; private set; } = "-";

    public DateOnly? FechaJornada { get; private set; }

    public List<string> FranjasGrafico { get; private set; } = [];

    public List<int> UsuariosGrafico { get; private set; } = [];

    public IReadOnlyList<EstadoVentanillaDto> Ventanillas { get; private set; } = [];

    public List<string> TramitesGrafico { get; private set; } = [];
    public List<int> DemandaTramitesGrafico { get; private set; } = [];

    public List<string> VentanillasGrafico { get; private set; } = [];
    public List<int> EsperaVentanillasGrafico { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await CargarDemandaAsync();
        await CargarVentanillasAsync();
        await CargarTramitesAsync();
    }

    private async Task CargarDemandaAsync()
    {
        FechaJornada = await context.RegistrosDemanda
            .AsNoTracking()
            .OrderByDescending(r => r.Fecha)
            .Select(r => (DateOnly?)r.Fecha)
            .FirstOrDefaultAsync();

        if (!FechaJornada.HasValue)
        {
            return;
        }

        var registros = await context.RegistrosDemanda
    .AsNoTracking()
    .Include(r => r.Tramite)
    .Where(r => r.Fecha == FechaJornada.Value)
    .ToListAsync();

        if (registros.Count == 0)
        {
            return;
        }

        DemandaTotal = registros.Sum(r =>
            r.CantidadUsuarios);

        var datosGrafico = registros
            .GroupBy(r => r.FranjaHoraria)
            .Select(grupo => new
            {
                Franja = grupo.Key,
                Total = grupo.Sum(r =>
                    r.CantidadUsuarios)
            })
            .OrderBy(x => x.Franja)
            .ToList();

        FranjasGrafico = datosGrafico
            .Select(x => x.Franja)
            .ToList();

        UsuariosGrafico = datosGrafico
            .Select(x => x.Total)
            .ToList();

        var mayor = datosGrafico
            .OrderByDescending(x => x.Total)
            .First();

        FranjaMayorDemanda = mayor.Franja;
        var demandaPorTramite = registros
    .GroupBy(r => r.Tramite.Nombre)
    .Select(grupo => new
    {
        Tramite = grupo.Key,
        Total = grupo.Sum(r => r.CantidadUsuarios)
    })
    .OrderByDescending(x => x.Total)
    .ToList();

        TramitesGrafico = demandaPorTramite
            .Select(x => x.Tramite)
            .ToList();

        DemandaTramitesGrafico = demandaPorTramite
            .Select(x => x.Total)
            .ToList();
    }

    private async Task CargarVentanillasAsync()
    {
        Ventanillas = await dynamicAssignmentServicio
            .ObtenerEstadosVentanillasAsync();

        PuntosActivos = Ventanillas.Count(v =>
            v.Estado == "Activa");

        TurnosPendientes = Ventanillas.Sum(v =>
            v.UsuariosEnCola);
        VentanillasGrafico = Ventanillas
    .Select(v => v.Nombre)
    .ToList();

        EsperaVentanillasGrafico = Ventanillas
            .Select(v => v.TiempoEsperaEstimadoMinutos)
            .ToList();
    }

    private async Task CargarTramitesAsync()
    {
        TramitesActivos = await context.Tramites
            .AsNoTracking()
            .CountAsync(t => t.Estado == "Activo");
    }
}