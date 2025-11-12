namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// Represents the concentration of an element, including its value and unit, as a value object.
    /// </summary>
    public class Concentration : ValueObject
    {
        /// <summary>
        /// Gets the numerical value of the concentration.
        /// </summary>
        public decimal Value { get; private set; }

        /// <summary>
        /// Gets the unit of measurement for the concentration (e.g., ppm, ppb).
        /// </summary>
        public string Unit { get; private set; }

        private Concentration(decimal value, string unit)
        {
            Value = value;
            Unit = unit;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Concentration"/> value object.
        /// </summary>
        /// <param name="value">The concentration value.</param>
        /// <param name="unit">The unit of measurement.</param>
        /// <returns>A new <see cref="Concentration"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the value is negative or the unit is empty.</exception>
        public static Concentration Create(decimal value, string unit)
        {
            if (value < 0)
                throw new ArgumentException("غلظت نمی‌تواند منفی باشد", nameof(value));

            if (string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("واحد نمی‌تواند خالی باشد", nameof(unit));

            return new Concentration(value, unit.Trim());
        }

        /// <summary>
        /// Creates a new <see cref="Concentration"/> instance with the unit set to "ppm" (parts per million).
        /// </summary>
        /// <param name="value">The concentration value.</param>
        /// <returns>A new <see cref="Concentration"/> instance in ppm.</returns>
        public static Concentration CreatePpm(decimal value)
        {
            return Create(value, "ppm");
        }

        /// <summary>
        /// Creates a new <see cref="Concentration"/> instance with the unit set to "ppb" (parts per billion).
        /// </summary>
        /// <param name="value">The concentration value.</param>
        /// <returns>A new <see cref="Concentration"/> instance in ppb.</returns>
        public static Concentration CreatePpb(decimal value)
        {
            return Create(value, "ppb");
        }

        /// <summary>
        /// Converts the concentration to a different unit. Currently supports conversion between ppm and ppb.
        /// </summary>
        /// <param name="targetUnit">The target unit to convert to.</param>
        /// <returns>A new <see cref="Concentration"/> instance with the converted value and unit.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the unit conversion is not supported.</exception>
        public Concentration ConvertTo(string targetUnit)
        {
            if (Unit.Equals(targetUnit, StringComparison.OrdinalIgnoreCase))
                return this;

            decimal convertedValue;

            // Convert ppm to ppb
            if (Unit.Equals("ppm", StringComparison.OrdinalIgnoreCase) && targetUnit.Equals("ppb", StringComparison.OrdinalIgnoreCase))
            {
                convertedValue = Value * 1000;
            }
            // Convert ppb to ppm
            else if (Unit.Equals("ppb", StringComparison.OrdinalIgnoreCase) && targetUnit.Equals("ppm", StringComparison.OrdinalIgnoreCase))
            {
                convertedValue = Value / 1000;
            }
            else
            {
                throw new InvalidOperationException($"تبدیل از {Unit} به {targetUnit} پشتیبانی نمی‌شود");
            }

            return Create(convertedValue, targetUnit);
        }

        /// <summary>
        /// Gets the components for value-based equality comparison.
        /// </summary>
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
            yield return Unit.ToLower();
        }

        /// <summary>
        /// Returns a string representation of the concentration.
        /// </summary>
        /// <returns>A formatted string in the format "Value Unit".</returns>
        public override string ToString()
        {
            return $"{Value:F4} {Unit}";
        }
    }
}