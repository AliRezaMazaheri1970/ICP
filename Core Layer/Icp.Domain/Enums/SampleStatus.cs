namespace Core.Icp.Domain.Enums
{
    /// <summary>
    /// وضعیت Sample
    /// </summary>
    public enum SampleStatus
    {
        /// <summary>
        /// در انتظار پردازش
        /// </summary>
        Pending = 1,

        /// <summary>
        /// در حال پردازش
        /// </summary>
        Processing = 2,

        /// <summary>
        /// پردازش شده
        /// </summary>
        Processed = 3,

        /// <summary>
        /// تایید شده
        /// </summary>
        Approved = 4,

        /// <summary>
        /// رد شده
        /// </summary>
        Rejected = 5,

        /// <summary>
        /// نیاز به بررسی مجدد
        /// </summary>
        RequiresReview = 6
    }
}