using Infrastructure.Icp.Files.Models;

namespace Infrastructure.Icp.Files.Interfaces
{
    /// <summary>
    /// رابط عمومی برای خواندن فایل
    /// </summary>
    public interface IFileReader<T> where T : class
    {
        Task<List<T>> ReadAsync(string filePath);
        Task<List<T>> ReadAsync(Stream stream);
        Task<FileValidationResult> ValidateAsync(string filePath);
    }
}