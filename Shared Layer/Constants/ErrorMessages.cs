namespace Shared.Icp.Constants
{
    /// <summary>
    /// پیام‌های خطا
    /// </summary>
    public static class ErrorMessages
    {
        // Sample Errors
        public const string SampleNotFound = "نمونه مورد نظر یافت نشد";
        public const string SampleIdDuplicate = "شناسه نمونه تکراری است";
        public const string SampleInvalidWeight = "وزن نمونه نامعتبر است";
        public const string SampleInvalidVolume = "حجم نمونه نامعتبر است";
        public const string SampleInvalidDilutionFactor = "ضریب رقت نامعتبر است";

        // Element Errors
        public const string ElementNotFound = "عنصر مورد نظر یافت نشد";
        public const string ElementSymbolDuplicate = "نماد عنصر تکراری است";
        public const string ElementInactive = "عنصر غیرفعال است";

        // CRM Errors
        public const string CRMNotFound = "CRM مورد نظر یافت نشد";
        public const string CRMIdDuplicate = "شناسه CRM تکراری است";
        public const string CRMExpired = "CRM منقضی شده است";
        public const string CRMValueNotFound = "مقدار تایید شده برای این عنصر یافت نشد";

        // Project Errors
        public const string ProjectNotFound = "پروژه مورد نظر یافت نشد";
        public const string ProjectNameDuplicate = "نام پروژه تکراری است";
        public const string ProjectHasSamples = "پروژه دارای نمونه است و قابل حذف نیست";

        // Calibration Errors
        public const string CalibrationCurveNotFound = "منحنی کالیبراسیون یافت نشد";
        public const string CalibrationInvalidRSquared = "ضریب همبستگی (R²) کمتر از حد مجاز است";
        public const string CalibrationInsufficientPoints = "تعداد نقاط کالیبراسیون کافی نیست";

        // Quality Check Errors
        public const string QualityCheckFailed = "کنترل کیفیت با خطا مواجه شد";
        public const string QualityCheckWeightOutOfRange = "وزن خارج از محدوده مجاز است";
        public const string QualityCheckVolumeOutOfRange = "حجم خارج از محدوده مجاز است";
        public const string QualityCheckDFOutOfRange = "ضریب رقت خارج از محدوده مجاز است";

        // File Processing Errors
        public const string FileNotFound = "فایل مورد نظر یافت نشد";
        public const string FileFormatInvalid = "فرمت فایل نامعتبر است";
        public const string FileEmpty = "فایل خالی است";
        public const string FileParsingError = "خطا در خواندن فایل";
        public const string FileColumnMissing = "ستون مورد نیاز در فایل یافت نشد";

        // General Errors
        public const string InvalidOperation = "عملیات نامعتبر است";
        public const string UnauthorizedAccess = "دسترسی غیرمجاز";
        public const string DatabaseError = "خطا در دیتابیس";
        public const string UnexpectedError = "خطای غیرمنتظره رخ داده است";
    }
}