namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public sealed record TurnoHistorialDto(
    int TurnoId,
    string CodigoTurno,
    string TramiteNombre,
    DateTime FechaHoraSolicitud,
    string? VentanillaInicial,
    DateTime? FechaHoraAsignacionInicial,
    string? VentanillaActual,
    DateTime? FechaHoraUltimaReasignacion,
    string Estado,
    DateTime? FechaHoraAtencion,
    DateTime? FechaHoraFinalizacion,
    IReadOnlyList<TurnoMovimientoDto> Movimientos);

public sealed record TurnoMovimientoDto(
    string TipoMovimiento,
    string? VentanillaOrigen,
    string? VentanillaDestino,
    DateTime FechaHoraMovimiento,
    string Motivo);
