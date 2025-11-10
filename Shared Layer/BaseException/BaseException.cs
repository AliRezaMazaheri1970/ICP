namespace Shared.Icp.Exceptions
{
    /// <summary>
    /// کلاس پایه برای تمام Exception های سفارشی
    /// </summary>
    public abstract class BaseException : Exception
    {
        public string Code { get; }
        public DateTime Timestamp { get; }

        protected BaseException(string message, string code)
            : base(message)
        {
            Code = code;
            Timestamp = DateTime.UtcNow;
        }

        protected BaseException(string message, string code, Exception innerException)
            : base(message, innerException)
        {
            Code = code;
            Timestamp = DateTime.UtcNow;
        }
    }
}