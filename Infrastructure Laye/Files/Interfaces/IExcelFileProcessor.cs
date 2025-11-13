using Infrastructure.Icp.Files.Models;

namespace Infrastructure.Icp.Files.Interfaces
{
    /// <summary>
    /// پردازش فایل‌های Excel
    /// </summary>
    public interface IExcelFileProcessor
    {
        Task<FileImportResult> ImportSamplesAsync(string filePath);
        Task<FileImportResult> ImportSamplesAsync(Stream stream, string fileName);
        Task<byte[]> ExportSamplesAsync<T>(IEnumerable<T> data, string sheetName = "Data") where T : class;
        Task ExportSamplesToFileAsync<T>(string filePath, IEnumerable<T> data, string sheetName = "Data") where T : class;
        Task<List<string>> GetSheetNamesAsync(string filePath);
    }
}