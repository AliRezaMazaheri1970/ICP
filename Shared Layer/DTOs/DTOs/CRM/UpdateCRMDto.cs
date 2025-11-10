using System.ComponentModel.DataAnnotations;

namespace Shared.Icp.DTOs.CRM
{
    /// <summary>
    /// DTO برای ویرایش CRM
    /// </summary>
    public class UpdateCRMDto
    {
        [Required(ErrorMessage = "شناسه الزامی است")]
        public int Id { get; set; }

        [Required(ErrorMessage = "نام CRM الزامی است")]
        [StringLength(200, ErrorMessage = "نام CRM نباید بیشتر از 200 کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "نام تولیدکننده نباید بیشتر از 100 کاراکتر باشد")]
        public string? Manufacturer { get; set; }

        [StringLength(50, ErrorMessage = "شماره سری نباید بیشتر از 50 کاراکتر باشد")]
        public string? LotNumber { get; set; }

        public DateTime? ExpirationDate { get; set; }

        public bool IsActive { get; set; }

        [StringLength(1000, ErrorMessage = "توضیحات نباید بیشتر از 1000 کاراکتر باشد")]
        public string? Notes { get; set; }
    }
}