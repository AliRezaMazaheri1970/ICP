using System.ComponentModel.DataAnnotations;

namespace Shared.Icp.DTOs.CRM
{
    /// <summary>
    /// DTO برای ایجاد مقدار تایید شده
    /// </summary>
    public class CreateCRMValueDto
    {
        [Required(ErrorMessage = "شناسه عنصر الزامی است")]
        public int ElementId { get; set; }

        [Required(ErrorMessage = "مقدار تایید شده الزامی است")]
        [Range(0, double.MaxValue, ErrorMessage = "مقدار تایید شده نمی‌تواند منفی باشد")]
        public decimal CertifiedValue { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "عدم قطعیت نمی‌تواند منفی باشد")]
        public decimal? Uncertainty { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "حد پایین نمی‌تواند منفی باشد")]
        public decimal? LowerLimit { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "حد بالا نمی‌تواند منفی باشد")]
        public decimal? UpperLimit { get; set; }

        [Required(ErrorMessage = "واحد الزامی است")]
        [StringLength(20, ErrorMessage = "واحد نباید بیشتر از 20 کاراکتر باشد")]
        public string Unit { get; set; } = "ppm";
    }
}