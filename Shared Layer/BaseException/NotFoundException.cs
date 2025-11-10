namespace Shared.Icp.Exceptions
{
    /// <summary>
    /// Exception برای موارد یافت نشده
    /// </summary>
    public class NotFoundException : BaseException
    {
        public NotFoundException(string message)
            : base(message, "NOT_FOUND")
        {
        }

        public NotFoundException(string entityName, object key)
            : base($"{entityName} با شناسه {key} یافت نشد", "NOT_FOUND")
        {
        }
    }
}