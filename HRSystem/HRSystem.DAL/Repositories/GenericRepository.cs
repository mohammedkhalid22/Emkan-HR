using HRSystem.DAL.Data;
using HRSystem.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.DAL.Repositories;

public interface IGenericRepository<T> where T : BaseEntity
{
    // Tracked query: use for edits
    IQueryable<T> Query();

    // Read-only query: use for lists and details
    IQueryable<T> QueryNoTracking();

    Task<T?> GetByIdAsync(int id);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> _set;

    public GenericRepository(HRDbContext context)
    {
        _set = context.Set<T>();
    }

    public IQueryable<T> Query() => _set;

    public IQueryable<T> QueryNoTracking() => _set.AsNoTracking();

    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);

    public async Task AddAsync(T entity) => await _set.AddAsync(entity);

    public void Update(T entity) => _set.Update(entity);

    // Becomes a soft delete in HRDbContext.SaveChangesAsync
    public void Remove(T entity) => _set.Remove(entity);
}
