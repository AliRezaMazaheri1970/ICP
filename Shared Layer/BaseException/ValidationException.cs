namespace Shared.Icp.Exceptions
{
    /// <summary>
    /// Exception برای خطاهای اعتبارسنجی
    /// </summary>
    public class ValidationException : BaseException
    {
        public Dictionary<string, List<string>> Errors { get; }

        public ValidationException(string message)
            : base(message, "VALIDATION_ERROR")
        {
            Errors = new Dictionary<string, List<string>>();
        }

        public ValidationException(Dictionary<string, List<string>> errors)
            : base("یک یا چند خطای اعتبارسنجی رخ داده است", "VALIDATION_ERROR")
        {
            Errors = errors;
        }

        public ValidationException(string field, string error)
            : base($"خطای اعتبارسنجی: {error}", "VALIDATION_ERROR")
        {
            Errors = new Dictionary<string, List<string>>
            {
                { field, new List<string> { error } }
            };
        }
    }
}