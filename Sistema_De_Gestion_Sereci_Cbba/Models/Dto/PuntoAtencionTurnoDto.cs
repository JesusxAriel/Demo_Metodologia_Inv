namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public sealed record PuntoAtencionTurnoDto(
    int Id,
    int Posicion,
    string CodigoTurno,
    string TramiteNombre,
    DateTime FechaHoraSolicitud);
