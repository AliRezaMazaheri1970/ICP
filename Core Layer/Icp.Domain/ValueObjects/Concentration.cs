namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// غلظت یک عنصر با واحد
    /// </summary>
    public class Concentration : ValueObject
    {
        public decimal Value { get; private set; }
        public string Unit { get; private set; }

        private Concentration(decimal value, string unit)
        {
            Value = value;
            Unit = unit;
        }

        /// <summary>
        /// ایجاد غلظت جدید
        /// </summary>
        public static Concentration Create(decimal value, string unit)
        {
            if (value < 0)
                throw new ArgumentException("غلظت نمی‌تواند منفی باشد", nameof(value));

            if (string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("واحد نمی‌تواند خالی باشد", nameof(unit));

            return new Concentration(value, unit.Trim());
        }

        /// <summary>
        /// ایجاد غلظت با واحد ppm
        /// </summary>
        public static Concentration CreatePpm(decimal value)
        {
            return Create(value, "ppm");
        }

        /// <summary>
        /// ایجاد غلظت با واحد ppb
        /// </summary>
        public static Concentration CreatePpb(decimal value)
        {
            return Create(value, "ppb");
        }

        /// <summary>
        /// تبدیل به واحد دیگر
        /// </summary>
        public Concentration ConvertTo(string targetUnit)
        {
            if (Unit == targetUnit)
                return this;

            decimal convertedValue = Value;

            // تبدیل ppm به ppb
            if (Unit.ToLower() == "ppm" && targetUnit.ToLower() == "ppb")
            {
                convertedValue = Value * 1000;
            }
            // تبدیل ppb به ppm
            else if (Unit.ToLower() == "ppb" && targetUnit.ToLower() == "ppm")
            {
                convertedValue = Value / 1000;
            }
            else
            {
                throw new InvalidOperationException($"تبدیل از {Unit} به {targetUnit} پشتیبانی نمی‌شود");
            }

            return Create(convertedValue, targetUnit);
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
            yield return Unit.ToLower();
        }

        public override string ToString()
        {
            return $"{Value:F4} {Unit}";
        }
    }
}