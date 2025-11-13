namespace Shared.Icp.Exceptions
{
    /// <summary>
    /// Exception برای خطاهای پردازش فایل
    /// </summary>
    public class FileProcessingException : BaseException
    {
        public string? FileName { get; }
        public int? LineNumber { get; }

        // Constructor 1: فقط پیام
        public FileProcessingException(string message)
            : base(message, "FILE_PROCESSING_ERROR")
        {
        }

        // Constructor 2: پیام + fileName
        public FileProcessingException(string message, string fileName)
            : base(message, "FILE_PROCESSING_ERROR")
        {
            FileName = fileName;
        }

        // Constructor 3: پیام + fileName + lineNumber
        public FileProcessingException(string message, string fileName, int lineNumber)
            : base($"{message} (فایل: {fileName}, خط: {lineNumber})", "FILE_PROCESSING_ERROR")
        {
            FileName = fileName;
            LineNumber = lineNumber;
        }

        // Constructor 4: پیام + innerException
        public FileProcessingException(string message, Exception innerException)
            : base(message, "FILE_PROCESSING_ERROR", innerException)
        {
        }

        // Constructor 5: پیام + fileName + innerException ← این مورد نیازه!
        public FileProcessingException(string message, string fileName, Exception innerException)
            : base(message, "FILE_PROCESSING_ERROR", innerException)
        {
            FileName = fileName;
        }

        // Constructor 6: همه پارامترها (اختیاری - برای کامل بودن)
        public FileProcessingException(string message, string fileName, int lineNumber, Exception innerException)
            : base($"{message} (فایل: {fileName}, خط: {lineNumber})", "FILE_PROCESSING_ERROR", innerException)
        {
            FileName = fileName;
            LineNumber = lineNumber;
        }
    }
}