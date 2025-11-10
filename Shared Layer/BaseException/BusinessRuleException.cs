namespace Shared.Icp.Exceptions
{
    /// <summary>
    /// Exception برای نقض قوانین کسب‌وکار
    /// </summary>
    public class BusinessRuleException : BaseException
    {
        public BusinessRuleException(string message)
            : base(message, "BUSINESS_RULE_VIOLATION")
        {
        }

        public BusinessRuleException(string message, Exception innerException)
            : base(message, "BUSINESS_RULE_VIOLATION", innerException)
        {
        }
    }
}