namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// شدت سیگنال (Net Intensity)
    /// </summary>
    public class Intensity : ValueObject
    {
        public decimal Value { get; private set; }

        private Intensity(decimal value)
        {
            Value = value;
        }

        /// <summary>
        /// ایجاد شدت جدید
        /// </summary>
        public static Intensity Create(decimal value)
        {
            if (value < 0)
                throw new ArgumentException("شدت نمی‌تواند منفی باشد", nameof(value));

            return new Intensity(value);
        }

        /// <summary>
        /// آیا شدت در محدوده معتبر است؟
        /// </summary>
        public bool IsValid(decimal minThreshold = 0)
        {
            return Value >= minThreshold;
        }

        /// <summary>
        /// آیا شدت بالاتر از حد است؟ (اشباع شده)
        /// </summary>
        public bool IsSaturated(decimal saturationThreshold)
        {
            return Value > saturationThreshold;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString()
        {
            return $"{Value:F2}";
        }
    }
}