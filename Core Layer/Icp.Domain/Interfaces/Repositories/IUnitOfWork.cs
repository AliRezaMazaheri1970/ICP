namespace Core.Icp.Domain.Interfaces.Repositories
{
    /// <summary>
    /// رابط Unit of Work برای مدیریت Transaction ها
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        // Repository ها
        ISampleRepository Samples { get; }
        IElementRepository Elements { get; }
        ICRMRepository CRMs { get; }
        IProjectRepository Projects { get; }

        // Transaction Operations
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}