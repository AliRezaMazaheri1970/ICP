using Core.Entity.Books;

namespace Core.Interfaces.Repositories.Books
{
    public interface IBookRepository : IRepository<Book>
    {
        Task<IEnumerable<Book>> SearchAsync(string searchTerm);
        Task<bool> IsISBNUniqueAsync(string isbn, int? excludeId = null);
        Task<Book?> GetByISBNAsync(string isbn);
    }
}