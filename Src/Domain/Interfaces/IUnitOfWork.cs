using Domain.Common;

namespace Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> CommitAsync(CancellationToken cancellationToken);
}