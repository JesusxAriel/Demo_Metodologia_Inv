using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;
using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Pages;

public class SimulacionModel(ApplicationDbContext context) : PageModel
{
    public const string MetodoConvencional = "Convencional";
    public const string MetodoDinamica = "Dinamica";

    [BindProperty]
    public EscenarioFormulario Formulario { get; set; } = new();

    public int PuntosActivosActuales { get; private set; }

    [TempData]
    public string? Mensaje { get; set; }

    public async Task OnGetAsync()
    {
        await CargarDatosAsync();
        Formulario.PuntosActivos = Math.Max(1, PuntosActivosActuales);
    }

    public async Task<IActionResult> OnPostEjecutarAsync()
    {
        Formulario.Nombre = Formulario.Nombre?.Trim() ?? string.Empty;
        ValidarFormulario();

        if (!ModelState.IsValid)
        {
            await CargarDatosAsync();
            return Page();
        }

        var escenario = new EscenarioSimulacion
        {
            Nombre = Formulario.Nombre,
            CantidadUsuariosEstimados = Formulario.CantidadUsuarios,
            PuntosActivos = Formulario.PuntosActivos,
            HoraInicio = Formulario.HoraInicio,
            HoraFin = Formulario.HoraFin,
            MetodoAsignacion = Formulario.MetodoAsignacion,
            FechaCreacion = DateTime.Now
        };

        try
        {
            await context.EscenariosSimulacion.AddAsync(escenario);
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty,
                "No se pudo registrar el escenario. Intente nuevamente.");
            await CargarDatosAsync();
            return Page();
        }

        Mensaje = $"El escenario «{escenario.Nombre}» se registró correctamente.";
        return RedirectToPage();
    }

    private void ValidarFormulario()
    {
        if (Formulario.MetodoAsignacion != MetodoConvencional &&
            Formulario.MetodoAsignacion != MetodoDinamica)
        {
            ModelState.AddModelError(
                $"{nameof(Formulario)}.{nameof(Formulario.MetodoAsignacion)}",
                "Seleccione un método de asignación válido.");
        }

        if (Formulario.HoraFin <= Formulario.HoraInicio)
        {
            ModelState.AddModelError(
                $"{nameof(Formulario)}.{nameof(Formulario.HoraFin)}",
                "La hora final debe ser posterior a la hora inicial.");
        }
    }

    private async Task CargarDatosAsync()
    {
        PuntosActivosActuales = await context.PuntosAtencion
            .AsNoTracking()
            .CountAsync(punto => punto.Estado == "Activa");
    }
}

public class EscenarioFormulario
{
    [Required(ErrorMessage = "Ingrese el nombre del escenario.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = "Escenario jornada regular";

    [Range(1, 100000, ErrorMessage = "Ingrese una cantidad de usuarios mayor que cero.")]
    public int CantidadUsuarios { get; set; } = 346;

    [Range(1, 100, ErrorMessage = "Ingrese al menos un punto de atención.")]
    public int PuntosActivos { get; set; } = 1;

    public TimeOnly HoraInicio { get; set; } = new(8, 0);

    public TimeOnly HoraFin { get; set; } = new(14, 0);

    [Required(ErrorMessage = "Seleccione un método de asignación.")]
    public string MetodoAsignacion { get; set; } = SimulacionModel.MetodoDinamica;
}
