namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// Represents a measurement value along with its associated uncertainty, encapsulated as a value object.
    /// </summary>
    public class Uncertainty : ValueObject
    {
        /// <summary>
        /// Gets the primary measurement value.
        /// </summary>
        public decimal Value { get; private set; }

        /// <summary>
        /// Gets the uncertainty associated with the measurement value.
        /// </summary>
        public decimal UncertaintyValue { get; private set; }

        /// <summary>
        /// Gets the unit of measurement for the value and uncertainty.
        /// </summary>
        public string Unit { get; private set; }

        private Uncertainty(decimal value, decimal uncertaintyValue, string unit)
        {
            Value = value;
            UncertaintyValue = uncertaintyValue;
            Unit = unit;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Uncertainty"/> value object.
        /// </summary>
        /// <param name="value">The measurement value.</param>
        /// <param name="uncertaintyValue">The associated uncertainty.</param>
        /// <param name="unit">The unit of measurement.</param>
        /// <returns>A new <see cref="Uncertainty"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the uncertainty value is negative.</exception>
        public static Uncertainty Create(decimal value, decimal uncertaintyValue, string unit = "")
        {
            if (uncertaintyValue < 0)
                throw new ArgumentException("Uncertainty cannot be negative.", nameof(uncertaintyValue));

            return new Uncertainty(value, uncertaintyValue, unit);
        }

        /// <summary>
        /// Gets the relative uncertainty expressed as a percentage.
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
        /// Converts the uncertainty into an <see cref="AcceptableRange"/>, representing the interval from (Value - Uncertainty) to (Value + Uncertainty).
        /// </summary>
        /// <returns>An <see cref="AcceptableRange"/> object.</returns>
        public AcceptableRange ToRange()
        {
            return AcceptableRange.Create(
                Value - UncertaintyValue,
                Value + UncertaintyValue
            );
        }

        /// <summary>
        /// Gets the components for value-based equality comparison.
        /// </summary>
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
            yield return UncertaintyValue;
            yield return Unit;
        }

        /// <summary>
        /// Returns a string representation of the uncertainty in the format "Value ± UncertaintyValue Unit".
        /// </summary>
        /// <returns>A formatted string representing the measurement and its uncertainty.</returns>
        public override string ToString()
        {
            return $"{Value:F4} ± {UncertaintyValue:F4} {Unit}".Trim();
        }
    }
}