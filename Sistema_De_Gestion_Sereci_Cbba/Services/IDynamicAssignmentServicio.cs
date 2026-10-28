using Sistema_De_Gestion_Sereci_Cbba.Models;

namespace Sistema_De_Gestion_Sereci_Cbba.Services;

/// <summary>
/// Expone las operaciones de asignación dinámica de ventanillas.
/// </summary>
public interface IDynamicAssignmentServicio
{
    Task<IReadOnlyList<EstadoVentanillaDto>> ObtenerEstadosVentanillasAsync(
        int? tramiteId = null);

    /// <summary>
    /// Recomienda la ventanilla activa compatible con menor espera y cola.
    /// </summary>
    /// <param name="tramiteId">Identificador del trámite solicitado.</param>
    /// <returns>Resultado de la recomendación.</returns>
    Task<ResultadoAsignacionDto> CalcularAsignacionAsync(int tramiteId);

    Task<ReasignacionResultadoDto> ReasignarTurnosPendientesAsync(
        int puntoAtencionOrigenId,
        string motivo);
}
