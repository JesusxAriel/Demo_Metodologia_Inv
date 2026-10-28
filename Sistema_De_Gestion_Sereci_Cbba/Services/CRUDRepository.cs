using Microsoft.EntityFrameworkCore;
using Sistema_De_Gestion_Sereci_Cbba.Data;

namespace Sistema_De_Gestion_Sereci_Cbba.Services;

public class CRUDRepository<T>(ApplicationDbContext context) : ICRUDRepository<T>
    where T : class
{
    private readonly DbSet<T> _entities = context.Set<T>();

    public async Task<IEnumerable<T>> ObtenerTodosAsync()
    {
        return await _entities.AsNoTracking().ToListAsync();
    }

    public async Task<T?> ObtenerPorIdAsync(int id)
    {
        return await _entities.FindAsync(id);
    }

    public async Task CrearAsync(T entidad)
    {
        ArgumentNullException.ThrowIfNull(entidad);

        await _entities.AddAsync(entidad);
        await context.SaveChangesAsync();
    }

    public async Task ActualizarAsync(T entidad)
    {
        ArgumentNullException.ThrowIfNull(entidad);

        _entities.Update(entidad);
        await context.SaveChangesAsync();
    }

    public async Task EliminarAsync(int id)
    {
        var entidad = await _entities.FindAsync(id);
        if (entidad is null)
        {
            return;
        }

        _entities.Remove(entidad);
        await context.SaveChangesAsync();
    }
}
