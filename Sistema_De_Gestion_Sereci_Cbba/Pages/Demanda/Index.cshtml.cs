using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class DemandaModel(ApplicationDbContext context) : PageModel
{
    public IReadOnlyList<RegistroDemanda> Registros { get; private set; } = [];

    public int DemandaTotal { get; private set; }

    public double PromedioDemanda { get; private set; }

    public string FranjaMayorDemanda { get; private set; } = "-";

    public async Task OnGetAsync()
    {
        Registros = await context.RegistrosDemanda
            .AsNoTracking()
            .Include(registro => registro.Tramite)
            .OrderByDescending(registro => registro.Fecha)
            .ThenBy(registro => registro.FranjaHoraria)
            .ThenBy(registro => registro.Tramite.Nombre)
            .ToListAsync();

        if (Registros.Count == 0)
        {
            return;
        }

        DemandaTotal = Registros.Sum(registro =>
            registro.CantidadUsuarios);

        PromedioDemanda = Registros.Average(registro =>
            registro.CantidadUsuarios);

        var mayorDemanda = Registros
            .GroupBy(registro => registro.FranjaHoraria)
            .Select(grupo => new
            {
                FranjaHoraria = grupo.Key,
                TotalUsuarios = grupo.Sum(registro =>
                    registro.CantidadUsuarios)
            })
            .OrderByDescending(resultado => resultado.TotalUsuarios)
            .FirstOrDefault();

        if (mayorDemanda is not null)
        {
            FranjaMayorDemanda = mayorDemanda.FranjaHoraria;
        }
    }
}