namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// Represents a time interval defined by a start and end date, encapsulated as a value object.
    /// </summary>
    public class DateRange : ValueObject
    {
        /// <summary>
        /// Gets the start date of the time interval.
        /// </summary>
        public DateTime StartDate { get; private set; }

        /// <summary>
        /// Gets the end date of the time interval.
        /// </summary>
        public DateTime EndDate { get; private set; }

        private DateRange(DateTime startDate, DateTime endDate)
        {
            StartDate = startDate;
            EndDate = endDate;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DateRange"/> value object.
        /// </summary>
        /// <param name="startDate">The start date of the range.</param>
        /// <param name="endDate">The end date of the range.</param>
        /// <returns>A new <see cref="DateRange"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the start date is after the end date.</exception>
        public static DateRange Create(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
                throw new ArgumentException("The start date cannot be after the end date.");

            return new DateRange(startDate, endDate);
        }

        /// <summary>
        /// Creates a <see cref="DateRange"/> that covers the entirety of the current day.
        /// </summary>
        /// <returns>A <see cref="DateRange"/> for today.</returns>
        public static DateRange Today()
        {
            var today = DateTime.Today;
            return Create(today, today.AddDays(1).AddSeconds(-1));
        }

        /// <summary>
        /// Creates a <see cref="DateRange"/> that covers the current week (from Sunday to Saturday).
        /// </summary>
        /// <returns>A <see cref="DateRange"/> for the current week.</returns>
        public static DateRange ThisWeek()
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7).AddSeconds(-1);
            return Create(startOfWeek, endOfWeek);
        }

        /// <summary>
        /// Creates a <see cref="DateRange"/> that covers the current month.
        /// </summary>
        /// <returns>A <see cref="DateRange"/> for the current month.</returns>
        public static DateRange ThisMonth()
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);
            return Create(startOfMonth, endOfMonth);
        }

        /// <summary>
        /// Determines whether a specified date falls within the current date range (inclusive).
        /// </summary>
        /// <param name="date">The date to check.</param>
        /// <returns>true if the date is within the range; otherwise, false.</returns>
        public bool Contains(DateTime date)
        {
            return date >= StartDate && date <= EndDate;
        }

        /// <summary>
        /// Gets the duration of the date range in whole days.
        /// </summary>
        public int DurationInDays => (EndDate - StartDate).Days;

        /// <summary>
        /// Gets the components for value-based equality comparison.
        /// </summary>
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return StartDate;
            yield return EndDate;
        }

        /// <summary>
        /// Returns a string representation of the date range.
        /// </summary>
        /// <returns>A string in the format "yyyy-MM-dd to yyyy-MM-dd".</returns>
        public override string ToString()
        {
            return $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}";
        }
    }
}