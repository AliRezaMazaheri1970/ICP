using System.ComponentModel.DataAnnotations;

namespace Shared.Icp.DTOs.Samples
{
    /// <summary>
    /// DTO برای ایجاد نمونه جدید
    /// </summary>
    public class CreateSampleDto
    {
        [Required(ErrorMessage = "شناسه نمونه الزامی است")]
        [StringLength(100, ErrorMessage = "شناسه نمونه نباید بیشتر از 100 کاراکتر باشد")]
        public string SampleId { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام نمونه الزامی است")]
        [StringLength(200, ErrorMessage = "نام نمونه نباید بیشتر از 200 کاراکتر باشد")]
        public string SampleName { get; set; } = string.Empty;

        public DateTime? RunDate { get; set; }

        [Required(ErrorMessage = "شناسه پروژه الزامی است")]
        public int ProjectId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "وزن نمی‌تواند منفی باشد")]
        public decimal? Weight { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "حجم نمی‌تواند منفی باشد")]
        public decimal? Volume { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "ضریب رقت نمی‌تواند منفی باشد")]
        public decimal? DilutionFactor { get; set; }

        [StringLength(1000, ErrorMessage = "توضیحات نباید بیشتر از 1000 کاراکتر باشد")]
        public string? Notes { get; set; }
    }
}