namespace Sistema_De_Gestion_Sereci_Cbba.Models;

/// <summary>
/// Resultado de la recomendación de una ventanilla para un trámite.
/// </summary>
public sealed record ResultadoAsignacionDto(
    int TramiteId,
    int? PuntoAtencionId,
    string? NombreVentanilla,
    int UsuariosEnCola,
    int TiempoEsperaEstimadoMinutos,
    string Compatibilidad,
    string Explicacion,
    string NivelCongestion);
