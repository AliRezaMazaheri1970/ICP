namespace Shared.Icp.Helpers.Extensions
{
    /// <summary>
    /// متدهای کمکی برای DateTime
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// تبدیل به رشته فارسی
        /// </summary>
        public static string ToPersianDate(this DateTime dateTime)
        {
            var persianCalendar = new System.Globalization.PersianCalendar();
            var year = persianCalendar.GetYear(dateTime);
            var month = persianCalendar.GetMonth(dateTime);
            var day = persianCalendar.GetDayOfMonth(dateTime);

            return $"{year:0000}/{month:00}/{day:00}";
        }

        /// <summary>
        /// تبدیل به رشته فارسی با ساعت
        /// </summary>
        public static string ToPersianDateTime(this DateTime dateTime)
        {
            return $"{dateTime.ToPersianDate()} {dateTime:HH:mm:ss}";
        }

        /// <summary>
        /// شروع روز
        /// </summary>
        public static DateTime StartOfDay(this DateTime dateTime)
        {
            return dateTime.Date;
        }

        /// <summary>
        /// پایان روز
        /// </summary>
        public static DateTime EndOfDay(this DateTime dateTime)
        {
            return dateTime.Date.AddDays(1).AddTicks(-1);
        }

        /// <summary>
        /// شروع ماه
        /// </summary>
        public static DateTime StartOfMonth(this DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, 1);
        }

        /// <summary>
        /// پایان ماه
        /// </summary>
        public static DateTime EndOfMonth(this DateTime dateTime)
        {
            return dateTime.StartOfMonth().AddMonths(1).AddTicks(-1);
        }

        /// <summary>
        /// محاسبه سن
        /// </summary>
        public static int CalculateAge(this DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;

            if (birthDate.Date > today.AddYears(-age))
                age--;

            return age;
        }

        /// <summary>
        /// بررسی قرار گرفتن در بازه
        /// </summary>
        public static bool IsBetween(this DateTime dateTime, DateTime start, DateTime end)
        {
            return dateTime >= start && dateTime <= end;
        }
    }
}