using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class IndexModel(ApplicationDbContext context) : PageModel
{
    public int DemandaTotal { get; private set; }

    public int PuntosActivos { get; private set; }

    public int TramitesActivos { get; private set; }

    public double UtilizacionPromedio { get; private set; }

    public string FranjaMayorDemanda { get; private set; } = "-";

    public List<string> FranjasGrafico { get; private set; } = [];

    public List<int> UsuariosGrafico { get; private set; } = [];

    public IReadOnlyList<PuntoAtencion> PuntosAtencion { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await CargarDemandaAsync();
        await CargarPuntosAtencionAsync();
        await CargarTramitesAsync();
    }

    private async Task CargarDemandaAsync()
    {
        var registros = await context.RegistrosDemanda
            .AsNoTracking()
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
    }

    private async Task CargarPuntosAtencionAsync()
    {
        PuntosAtencion = await context.PuntosAtencion
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .ToListAsync();

        PuntosActivos = PuntosAtencion.Count(p =>
            p.Estado == "Activa");

        if (PuntosAtencion.Count > 0)
        {
            UtilizacionPromedio = PuntosAtencion
                .Average(p => p.CargaActualPorcentaje);
        }
    }

    private async Task CargarTramitesAsync()
    {
        TramitesActivos = await context.Tramites
            .AsNoTracking()
            .CountAsync(t => t.Estado == "Activo");
    }
}