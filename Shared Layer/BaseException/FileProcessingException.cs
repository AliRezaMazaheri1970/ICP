namespace Shared.Icp.Exceptions
{
    /// <summary>
    /// Exception برای خطاهای پردازش فایل
    /// </summary>
    public class FileProcessingException : BaseException
    {
        public string? FileName { get; }
        public int? LineNumber { get; }

        public FileProcessingException(string message)
            : base(message, "FILE_PROCESSING_ERROR")
        {
        }

        public FileProcessingException(string message, string fileName)
            : base(message, "FILE_PROCESSING_ERROR")
        {
            FileName = fileName;
        }

        public FileProcessingException(string message, string fileName, int lineNumber)
            : base($"{message} (فایل: {fileName}, خط: {lineNumber})", "FILE_PROCESSING_ERROR")
        {
            FileName = fileName;
            LineNumber = lineNumber;
        }

        public FileProcessingException(string message, Exception innerException)
            : base(message, "FILE_PROCESSING_ERROR", innerException)
        {
        }
    }
}