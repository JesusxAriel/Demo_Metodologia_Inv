namespace Sistema_De_Gestion_Sereci_Cbba.Models;

/// <summary>
/// Estado operativo de una ventanilla para el dashboard.
/// </summary>
public sealed record EstadoVentanillaDto(
    int Id,
    string Nombre,
    int UsuariosEnCola,
    int TiempoEsperaEstimadoMinutos,
    string Estado,
    bool EsCompatible,
    string NivelCongestion,
    bool PuedeRecibirAsignaciones);
