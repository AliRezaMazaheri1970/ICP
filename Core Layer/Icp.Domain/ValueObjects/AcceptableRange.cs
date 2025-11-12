namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// Represents an acceptable range for a value, defined by a minimum and maximum, encapsulated as a value object.
    /// </summary>
    public class AcceptableRange : ValueObject
    {
        /// <summary>
        /// Gets the minimum value of the acceptable range.
        /// </summary>
        public decimal MinValue { get; private set; }

        /// <summary>
        /// Gets the maximum value of the acceptable range.
        /// </summary>
        public decimal MaxValue { get; private set; }

        private AcceptableRange(decimal minValue, decimal maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AcceptableRange"/> value object.
        /// </summary>
        /// <param name="minValue">The minimum value of the range.</param>
        /// <param name="maxValue">The maximum value of the range.</param>
        /// <returns>A new <see cref="AcceptableRange"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the minimum value is greater than the maximum value.</exception>
        public static AcceptableRange Create(decimal minValue, decimal maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentException("حداقل نمی‌تواند بیشتر از حداکثر باشد");

            return new AcceptableRange(minValue, maxValue);
        }

        /// <summary>
        /// Creates an <see cref="AcceptableRange"/> from a center value and a percentage deviation.
        /// </summary>
        /// <param name="centerValue">The central value of the range.</param>
        /// <param name="deviationPercent">The allowed deviation from the center value, as a percentage (0-100).</param>
        /// <returns>A new <see cref="AcceptableRange"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the deviation percentage is not between 0 and 100.</exception>
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
        /// Determines whether a specified value falls within the current range (inclusive).
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>true if the value is within the range; otherwise, false.</returns>
        public bool IsInRange(decimal value)
        {
            return value >= MinValue && value <= MaxValue;
        }

        /// <summary>
        /// Calculates the percentage by which a value deviates from the center of the range.
        /// Returns 0 if the value is within the range.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>The deviation percentage, or null if the range size is zero.</returns>
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

        /// <summary>
        /// Gets the components for value-based equality comparison.
        /// </summary>
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return MinValue;
            yield return MaxValue;
        }

        /// <summary>
        /// Returns a string representation of the acceptable range.
        /// </summary>
        /// <returns>A formatted string in the format "[MinValue - MaxValue]".</returns>
        public override string ToString()
        {
            return $"[{MinValue:F4} - {MaxValue:F4}]";
        }
    }
}