namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// Represents the net intensity of a signal, typically from a measurement device, encapsulated as a value object.
    /// </summary>
    public class Intensity : ValueObject
    {
        /// <summary>
        /// Gets the numerical value of the signal intensity.
        /// </summary>
        public decimal Value { get; private set; }

        private Intensity(decimal value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Intensity"/> value object.
        /// </summary>
        /// <param name="value">The intensity value.</param>
        /// <returns>A new <see cref="Intensity"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the intensity value is negative.</exception>
        public static Intensity Create(decimal value)
        {
            if (value < 0)
                throw new ArgumentException("Intensity cannot be negative.", nameof(value));

            return new Intensity(value);
        }

        /// <summary>
        /// Checks if the intensity is valid by comparing it against a minimum threshold.
        /// </summary>
        /// <param name="minThreshold">The minimum acceptable value for the intensity. Defaults to 0.</param>
        /// <returns>true if the intensity is greater than or equal to the threshold; otherwise, false.</returns>
        public bool IsValid(decimal minThreshold = 0)
        {
            return Value >= minThreshold;
        }

        /// <summary>
        /// Determines if the signal intensity is saturated by checking if it exceeds a specified threshold.
        /// </summary>
        /// <param name="saturationThreshold">The value above which the signal is considered saturated.</param>
        /// <returns>true if the intensity is greater than the saturation threshold; otherwise, false.</returns>
        public bool IsSaturated(decimal saturationThreshold)
        {
            return Value > saturationThreshold;
        }

        /// <summary>
        /// Gets the components for value-based equality comparison.
        /// </summary>
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
        }

        /// <summary>
        /// Returns a string representation of the intensity value, formatted to two decimal places.
        /// </summary>
        /// <returns>A formatted string representing the intensity.</returns>
        public override string ToString()
        {
            return $"{Value:F2}";
        }
    }
}