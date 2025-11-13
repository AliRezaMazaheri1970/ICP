namespace Infrastructure.Icp.Files.Interfaces
{
    /// <summary>
    /// رابط عمومی برای نوشتن فایل
    /// </summary>
    public interface IFileWriter<T> where T : class
    {
        Task WriteAsync(string filePath, IEnumerable<T> data);
        Task<byte[]> WriteToStreamAsync(IEnumerable<T> data);
    }
}