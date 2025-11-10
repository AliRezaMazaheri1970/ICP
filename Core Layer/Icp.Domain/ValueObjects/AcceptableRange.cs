namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// محدوده قابل قبول برای مقادیر
    /// </summary>
    public class AcceptableRange : ValueObject
    {
        public decimal MinValue { get; private set; }
        public decimal MaxValue { get; private set; }

        private AcceptableRange(decimal minValue, decimal maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        /// <summary>
        /// ایجاد محدوده جدید
        /// </summary>
        public static AcceptableRange Create(decimal minValue, decimal maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentException("حداقل نمی‌تواند بیشتر از حداکثر باشد");

            return new AcceptableRange(minValue, maxValue);
        }

        /// <summary>
        /// ایجاد محدوده با مقدار مرکزی و انحراف درصدی
        /// </summary>
        public static AcceptableRange CreateFromDeviation(decimal centerValue, decimal deviationPercent)
        {
            if (deviationPercent < 0 || deviationPercent > 100)
                throw new ArgumentException("انحراف باید بین 0 تا 100 باشد", nameof(deviationPercent));

            var deviation = centerValue * (deviationPercent / 100);
            var minValue = centerValue - deviation;
            var maxValue = centerValue + deviation;

            return new AcceptableRange(minValue, maxValue);
        }

        /// <summary>
        /// بررسی قرار گرفتن مقدار در محدوده
        /// </summary>
        public bool IsInRange(decimal value)
        {
            return value >= MinValue && value <= MaxValue;
        }

        /// <summary>
        /// محاسبه انحراف از محدوده (درصد)
        /// </summary>
        public decimal? CalculateDeviationPercent(decimal value)
        {
            if (IsInRange(value))
                return 0;

            var centerValue = (MinValue + MaxValue) / 2;
            var deviation = Math.Abs(value - centerValue);
            var rangeSize = (MaxValue - MinValue) / 2;

            if (rangeSize == 0)
                return null;

            return (deviation / rangeSize) * 100;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return MinValue;
            yield return MaxValue;
        }

        public override string ToString()
        {
            return $"[{MinValue:F4} - {MaxValue:F4}]";
        }
    }
}