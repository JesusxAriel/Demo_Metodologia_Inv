namespace Sistema_De_Gestion_Sereci_Cbba.Services;

public interface ICRUDRepository<T> where T : class
{
    Task<IEnumerable<T>> ObtenerTodosAsync();
    Task<T?> ObtenerPorIdAsync(int id);
    Task CrearAsync(T entidad);
    Task ActualizarAsync(T entidad);
    Task EliminarAsync(int id);
}
