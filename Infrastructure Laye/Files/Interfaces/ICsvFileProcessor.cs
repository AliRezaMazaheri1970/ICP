using Infrastructure.Icp.Files.Models;

namespace Infrastructure.Icp.Files.Interfaces
{
    /// <summary>
    /// پردازش فایل‌های CSV
    /// </summary>
    public interface ICsvFileProcessor
    {
        Task<FileImportResult> ImportSamplesAsync(string filePath);
        Task<FileImportResult> ImportSamplesAsync(Stream stream, string fileName);
        Task<byte[]> ExportSamplesAsync<T>(IEnumerable<T> data) where T : class;
        Task ExportSamplesToFileAsync<T>(string filePath, IEnumerable<T> data) where T : class;
    }
}