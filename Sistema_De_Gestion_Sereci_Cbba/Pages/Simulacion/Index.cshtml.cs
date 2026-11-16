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
    public FormularioSimulacion Formulario { get; set; } = new();

    [TempData]
    public string? Mensaje { get; set; }

    /// <summary>Cantidad de ventanillas registradas (límite superior del escenario).</summary>
    public int PuntosRegistrados { get; private set; }

    public async Task OnGetAsync()
    {
        await CargarDatosAsync();

        // Valores iniciales: usa las ventanillas operativas actuales.
        var activas = await context.PuntosAtencion
            .AsNoTracking()
            .CountAsync(punto => punto.Estado == "Activa");
        Formulario.PuntosActivos = Math.Clamp(activas, 1, Math.Max(PuntosRegistrados, 1));
    }

    public async Task<IActionResult> OnPostEjecutarAsync()
    {
        await CargarDatosAsync();
        ValidarFormulario();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var escenario = new EscenarioSimulacion
        {
            Nombre = Formulario.Nombre!.Trim(),
            CantidadUsuariosEstimados = Formulario.CantidadUsuarios,
            PuntosActivos = Formulario.PuntosActivos,
            HoraInicio = Formulario.HoraInicio,
            HoraFin = Formulario.HoraFin,
            MetodoAsignacion = Formulario.MetodoAsignacion!,
            FechaCreacion = DateTime.Now
        };

        try
        {
            await context.EscenariosSimulacion.AddAsync(escenario);
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                string.Empty,
                "No fue posible guardar el escenario. Intente nuevamente.");
            return Page();
        }

        Mensaje = $"El escenario «{escenario.Nombre}» se registró correctamente " +
                  $"(#{escenario.Id}) con el método " +
                  $"{(escenario.MetodoAsignacion == MetodoDinamica ? "asignación dinámica" : "distribución convencional")}.";
        return RedirectToPage();
    }

    public static string FormatearDuracion(TimeOnly inicio, TimeOnly fin)
    {
        var minutos = (int)(fin - inicio).TotalMinutes;
        if (minutos <= 0)
        {
            return "—";
        }

        var horas = minutos / 60;
        var resto = minutos % 60;
        var textoHoras = horas == 1 ? "1 hora" : $"{horas} horas";
        return resto == 0
            ? textoHoras
            : horas == 0 ? $"{resto} min" : $"{horas} h {resto} min";
    }

    private async Task CargarDatosAsync()
    {
        PuntosRegistrados = await context.PuntosAtencion
            .AsNoTracking()
            .CountAsync();
    }

    private void ValidarFormulario()
    {
        if (string.IsNullOrWhiteSpace(Formulario.Nombre))
        {
            ModelState.AddModelError(
                "Formulario.Nombre", "El nombre del escenario es obligatorio.");
        }
        else if (Formulario.Nombre.Trim().Length > 100)
        {
            ModelState.AddModelError(
                "Formulario.Nombre", "El nombre no puede superar los 100 caracteres.");
        }

        if (Formulario.CantidadUsuarios <= 0)
        {
            ModelState.AddModelError(
                "Formulario.CantidadUsuarios",
                "La cantidad de usuarios debe ser mayor que cero.");
        }

        if (Formulario.PuntosActivos <= 0)
        {
            ModelState.AddModelError(
                "Formulario.PuntosActivos",
                "Debe haber al menos un punto de atención.");
        }
        else if (PuntosRegistrados > 0 && Formulario.PuntosActivos > PuntosRegistrados)
        {
            ModelState.AddModelError(
                "Formulario.PuntosActivos",
                $"Solo existen {PuntosRegistrados} puntos de atención registrados.");
        }

        if (Formulario.HoraFin <= Formulario.HoraInicio)
        {
            ModelState.AddModelError(
                "Formulario.HoraFin",
                "La hora final debe ser posterior a la hora inicial.");
        }

        if (Formulario.MetodoAsignacion is not (MetodoConvencional or MetodoDinamica))
        {
            ModelState.AddModelError(
                "Formulario.MetodoAsignacion", "Seleccione un método de asignación válido.");
        }
    }

    public sealed class FormularioSimulacion
    {
        public string? Nombre { get; set; } = "Escenario jornada regular";
        public int CantidadUsuarios { get; set; } = 346;
        public int PuntosActivos { get; set; } = 4;
        public TimeOnly HoraInicio { get; set; } = new(8, 0);
        public TimeOnly HoraFin { get; set; } = new(14, 0);
        public string? MetodoAsignacion { get; set; } = MetodoDinamica;
    }
}
