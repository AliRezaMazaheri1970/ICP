namespace Core.Icp.Domain.ValueObjects
{
    /// <summary>
    /// بازه زمانی
    /// </summary>
    public class DateRange : ValueObject
    {
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        private DateRange(DateTime startDate, DateTime endDate)
        {
            StartDate = startDate;
            EndDate = endDate;
        }

        /// <summary>
        /// ایجاد بازه زمانی جدید
        /// </summary>
        public static DateRange Create(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
                throw new ArgumentException("تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد");

            return new DateRange(startDate, endDate);
        }

        /// <summary>
        /// ایجاد بازه برای امروز
        /// </summary>
        public static DateRange Today()
        {
            var today = DateTime.Today;
            return Create(today, today.AddDays(1).AddSeconds(-1));
        }

        /// <summary>
        /// ایجاد بازه برای هفته جاری
        /// </summary>
        public static DateRange ThisWeek()
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7).AddSeconds(-1);
            return Create(startOfWeek, endOfWeek);
        }

        /// <summary>
        /// ایجاد بازه برای ماه جاری
        /// </summary>
        public static DateRange ThisMonth()
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);
            return Create(startOfMonth, endOfMonth);
        }

        /// <summary>
        /// بررسی قرار گرفتن تاریخ در بازه
        /// </summary>
        public bool Contains(DateTime date)
        {
            return date >= StartDate && date <= EndDate;
        }

        /// <summary>
        /// تعداد روزهای بازه
        /// </summary>
        public int DurationInDays => (EndDate - StartDate).Days;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return StartDate;
            yield return EndDate;
        }

        public override string ToString()
        {
            return $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}";
        }
    }
}