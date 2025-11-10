namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// عدم قطعیت یک اندازه‌گیری
    /// </summary>
    public class Uncertainty : ValueObject
    {
        public decimal Value { get; private set; }
        public decimal UncertaintyValue { get; private set; }
        public string Unit { get; private set; }

        private Uncertainty(decimal value, decimal uncertaintyValue, string unit)
        {
            Value = value;
            UncertaintyValue = uncertaintyValue;
            Unit = unit;
        }

        /// <summary>
        /// ایجاد عدم قطعیت جدید
        /// </summary>
        public static Uncertainty Create(decimal value, decimal uncertaintyValue, string unit = "")
        {
            if (uncertaintyValue < 0)
                throw new ArgumentException("عدم قطعیت نمی‌تواند منفی باشد", nameof(uncertaintyValue));

            return new Uncertainty(value, uncertaintyValue, unit);
        }

        /// <summary>
        /// عدم قطعیت نسبی (درصد)
        /// </summary>
        public decimal RelativeUncertaintyPercent
        {
            get
            {
                if (Value == 0)
                    return 0;

                return (UncertaintyValue / Math.Abs(Value)) * 100;
            }
        }

        /// <summary>
        /// محدوده با احتساب عدم قطعیت
        /// </summary>
        public AcceptableRange ToRange()
        {
            return AcceptableRange.Create(
                Value - UncertaintyValue,
                Value + UncertaintyValue
            );
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
            yield return UncertaintyValue;
            yield return Unit;
        }

        public override string ToString()
        {
            return $"{Value:F4} ± {UncertaintyValue:F4} {Unit}".Trim();
        }
    }
}