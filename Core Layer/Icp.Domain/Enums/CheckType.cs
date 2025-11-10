namespace Core.Icp.Domain.Enums
{
    /// <summary>
    /// نوع کنترل کیفیت
    /// </summary>
    public enum CheckType
    {
        /// <summary>
        /// بررسی وزن
        /// </summary>
        WeightCheck = 1,

        /// <summary>
        /// بررسی حجم
        /// </summary>
        VolumeCheck = 2,

        /// <summary>
        /// بررسی ضریب رقت
        /// </summary>
        DilutionFactorCheck = 3,

        /// <summary>
        /// بررسی سطرهای خالی
        /// </summary>
        EmptyCheck = 4,

        /// <summary>
        /// بررسی CRM
        /// </summary>
        CRMCheck = 5,

        /// <summary>
        /// کالیبراسیون Drift
        /// </summary>
        DriftCalibration = 6
    }
}