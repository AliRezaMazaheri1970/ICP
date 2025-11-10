namespace Shared.Icp.Constants
{
    /// <summary>
    /// ثابت‌های اعتبارسنجی
    /// </summary>
    public static class ValidationConstants
    {
        // String Lengths
        public const int SampleIdMaxLength = 100;
        public const int SampleNameMaxLength = 200;
        public const int ElementSymbolMaxLength = 10;
        public const int ElementNameMaxLength = 100;
        public const int CRMIdMaxLength = 50;
        public const int CRMNameMaxLength = 200;
        public const int ProjectNameMaxLength = 200;
        public const int NotesMaxLength = 1000;

        // Numeric Ranges
        public const decimal MinWeight = 0.0001m; // گرم
        public const decimal MaxWeight = 10000m; // گرم
        public const decimal MinVolume = 0.001m; // میلی‌لیتر
        public const decimal MaxVolume = 100000m; // میلی‌لیتر
        public const decimal MinDilutionFactor = 1m;
        public const decimal MaxDilutionFactor = 10000m;

        // Concentration Ranges
        public const decimal MinConcentration = 0m;
        public const decimal MaxConcentration = 1000000m; // ppm

        // Intensity Ranges
        public const decimal MinIntensity = 0m;
        public const decimal MaxIntensity = 100000000m;

        // Atomic Number Ranges
        public const int MinAtomicNumber = 1;
        public const int MaxAtomicNumber = 118;

        // R² Ranges
        public const decimal MinRSquared = 0m;
        public const decimal MaxRSquared = 1m;
    }
}