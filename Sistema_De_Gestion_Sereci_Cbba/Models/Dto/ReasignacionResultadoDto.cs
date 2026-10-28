namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public sealed record ReasignacionResultadoDto(
    int Reasignados,
    IReadOnlyList<string> Bloqueados,
    bool TieneTurnosEnAtencion);
