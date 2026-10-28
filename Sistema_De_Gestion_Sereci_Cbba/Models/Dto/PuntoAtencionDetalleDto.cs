namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public sealed record PuntoAtencionDetalleDto(
    int Id,
    string Nombre,
    string Estado,
    bool PuedeRecibirAsignaciones,
    int TurnosPendientes,
    int TiposTramiteEnCola,
    int AtencionesEnCurso,
    IReadOnlyList<PuntoAtencionTramiteDto> TramitesCompatibles,
    IReadOnlyList<PuntoAtencionTramiteDto> TramitesEnCola,
    IReadOnlyList<PuntoAtencionTurnoDto> TurnosEnCola);
