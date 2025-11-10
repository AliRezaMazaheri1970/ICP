namespace Shared.Icp.Helpers.Extensions
{
    /// <summary>
    /// متدهای کمکی برای string
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// بررسی null یا خالی بودن
        /// </summary>
        public static bool IsNullOrEmpty(this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// بررسی null یا whitespace بودن
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Trim با بررسی null
        /// </summary>
        public static string? SafeTrim(this string? value)
        {
            return value?.Trim();
        }

        /// <summary>
        /// تبدیل به عدد با مدیریت خطا
        /// </summary>
        public static decimal? ToDecimalOrNull(this string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (decimal.TryParse(value, out var result))
                return result;

            return null;
        }

        /// <summary>
        /// تبدیل به عدد صحیح با مدیریت خطا
        /// </summary>
        public static int? ToIntOrNull(this string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(value, out var result))
                return result;

            return null;
        }

        /// <summary>
        /// حذف کاراکترهای خاص
        /// </summary>
        public static string RemoveSpecialCharacters(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return new string(value.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
        }

        /// <summary>
        /// محدود کردن طول رشته
        /// </summary>
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}