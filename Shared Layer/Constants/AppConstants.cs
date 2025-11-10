namespace Shared.Icp.Constants
{
    /// <summary>
    /// ثابت‌های برنامه
    /// </summary>
    public static class AppConstants
    {
        // Application Info
        public const string ApplicationName = "Isatis.NET";
        public const string ApplicationVersion = "1.0.0";

        // Units
        public const string UnitPpm = "ppm";
        public const string UnitPpb = "ppb";
        public const string UnitPercent = "%";
        public const string UnitGram = "g";
        public const string UnitMilliliter = "mL";

        // File Formats
        public const string CsvExtension = ".csv";
        public const string ExcelExtension = ".xlsx";
        public const string ExcelLegacyExtension = ".xls";
        public const string PdfExtension = ".pdf";
        public const string ProjectExtension = ".icp";

        // Pagination
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;

        // Quality Check Thresholds
        public const decimal DefaultWeightTolerance = 5.0m; // درصد
        public const decimal DefaultVolumeTolerance = 5.0m; // درصد
        public const decimal DefaultDFTolerance = 10.0m; // درصد
        public const decimal DefaultCRMTolerance = 10.0m; // درصد

        // Calibration
        public const decimal MinAcceptableRSquared = 0.995m;
        public const int MinCalibrationPoints = 3;
        public const int MaxCalibrationPoints = 10;

        // Date Formats
        public const string DateFormat = "yyyy-MM-dd";
        public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        public const string TimeFormat = "HH:mm:ss";

        // Sample Status
        public const string StatusPending = "Pending";
        public const string StatusProcessing = "Processing";
        public const string StatusProcessed = "Processed";
        public const string StatusApproved = "Approved";
        public const string StatusRejected = "Rejected";
        public const string StatusRequiresReview = "RequiresReview";

        // Check Types
        public const string CheckTypeWeight = "WeightCheck";
        public const string CheckTypeVolume = "VolumeCheck";
        public const string CheckTypeDF = "DilutionFactorCheck";
        public const string CheckTypeEmpty = "EmptyCheck";
        public const string CheckTypeCRM = "CRMCheck";
        public const string CheckTypeDrift = "DriftCalibration";

        // Check Status
        public const string CheckStatusPass = "Pass";
        public const string CheckStatusFail = "Fail";
        public const string CheckStatusWarning = "Warning";
        public const string CheckStatusPending = "Pending";
    }
}