namespace Core.Icp.Domain.Enums
{
    /// <summary>
    /// وضعیت نتیجه کنترل کیفیت
    /// </summary>
    public enum CheckStatus
    {
        /// <summary>
        /// قبول
        /// </summary>
        Pass = 1,

        /// <summary>
        /// رد
        /// </summary>
        Fail = 2,

        /// <summary>
        /// هشدار
        /// </summary>
        Warning = 3,

        /// <summary>
        /// در انتظار
        /// </summary>
        Pending = 4
    }
}