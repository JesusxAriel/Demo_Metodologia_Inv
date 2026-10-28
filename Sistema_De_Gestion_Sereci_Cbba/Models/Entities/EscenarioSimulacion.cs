namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public class EscenarioSimulacion
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int CantidadUsuariosEstimados { get; set; }
    public int PuntosActivos { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public string MetodoAsignacion { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }

    public ICollection<TurnoSimulado> TurnosSimulados { get; set; } = [];
}
