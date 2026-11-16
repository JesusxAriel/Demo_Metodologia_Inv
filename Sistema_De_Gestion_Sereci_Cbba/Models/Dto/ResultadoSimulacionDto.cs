namespace Sistema_De_Gestion_Sereci_Cbba.Models;

public sealed record MetricaVentanilla(
    string Nombre,
    int Atenciones,
    int MinutosOcupada,
    int OcupacionPorcentaje);

public sealed record MetricaHora(string Etiqueta, double EsperaPromedioMinutos);

public sealed record ResultadoMetodo(
    string Metodo,
    int Atendidos,
    int SinVentanillaCompatible,
    double EsperaPromedioMinutos,
    int EsperaMaximaMinutos,
    int EsperaP90Minutos,
    int MinutosExtraTrasCierre,
    double DispersionCarga,
    IReadOnlyList<int> DistribucionEspera,
    IReadOnlyList<MetricaVentanilla> Ventanillas,
    IReadOnlyList<MetricaHora> PorHora);

public sealed record ComparativaSimulacion(
    ResultadoMetodo Convencional,
    ResultadoMetodo Dinamica);
