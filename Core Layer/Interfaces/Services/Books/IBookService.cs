namespace Core.Interfaces.Services.Books
{
    public interface IBookService
    {
        Task<IEnumerable<object>> GetAllBooksAsync();
        Task<object?> GetBookByIdAsync(int id);
        Task<IEnumerable<object>> SearchBooksAsync(string searchTerm);
        Task<object> CreateBookAsync(object createDto);
        Task<object> UpdateBookAsync(int id, object updateDto);
        Task<bool> DeleteBookAsync(int id);
        Task<bool> ExistsAsync(int id);
    }
}