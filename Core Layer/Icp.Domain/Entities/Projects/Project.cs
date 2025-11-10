using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Elements;
using Core.Icp.Domain.Entities.Samples;

namespace Core.Icp.Domain.Entities.Projects
{
    /// <summary>
    /// پروژه تحلیل
    /// </summary>
    public class Project : BaseEntity
    {
        /// <summary>
        /// نام پروژه
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// توضیحات پروژه
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// نام فایل اصلی
        /// </summary>
        public string? SourceFileName { get; set; }

        /// <summary>
        /// تاریخ شروع
        /// </summary>
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// تاریخ پایان
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// نمونه‌های پروژه
        /// </summary>
        public virtual ICollection<Sample> Samples { get; set; } = new List<Sample>();

        /// <summary>
        /// منحنی‌های کالیبراسیون
        /// </summary>
        public virtual ICollection<CalibrationCurve> CalibrationCurves { get; set; } = new List<CalibrationCurve>();

        /// <summary>
        /// تنظیمات پروژه (JSON)
        /// </summary>
        public string? SettingsJson { get; set; }

        /// <summary>
        /// وضعیت پروژه
        /// </summary>
        public string Status { get; set; } = "Active";
    }
}