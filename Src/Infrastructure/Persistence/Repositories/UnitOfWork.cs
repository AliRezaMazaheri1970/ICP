using Domain.Common;
using Domain.Interfaces;
using System.Collections;

namespace Infrastructure.Persistence.Repositories;

public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    private Hashtable? _repositories;
    private bool _disposed;

    public IGenericRepository<T> Repository<T>() where T : BaseEntity
    {
        if (_repositories == null)
            _repositories = new Hashtable();

        var type = typeof(T).Name;

        // بررسی اینکه آیا قبلاً ریپازیتوری برای این Entity ساخته شده یا خیر
        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(GenericRepository<>);

            // ساخت یک نمونه جدید از GenericRepository برای Entity مورد نظر
            var repositoryInstance = Activator.CreateInstance(
                repositoryType.MakeGenericType(typeof(T)),
                context
            );

            _repositories.Add(type, repositoryInstance);
        }

        return (IGenericRepository<T>)_repositories[type]!;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                context.Dispose();
            }
        }
        _disposed = true;
    }
}