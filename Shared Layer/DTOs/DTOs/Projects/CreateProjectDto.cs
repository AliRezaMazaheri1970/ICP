using System.ComponentModel.DataAnnotations;

namespace Shared.Icp.DTOs.Projects
{
    /// <summary>
    /// DTO برای ایجاد پروژه جدید
    /// </summary>
    public class CreateProjectDto
    {
        [Required(ErrorMessage = "نام پروژه الزامی است")]
        [StringLength(200, ErrorMessage = "نام پروژه نباید بیشتر از 200 کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "توضیحات نباید بیشتر از 1000 کاراکتر باشد")]
        public string? Description { get; set; }

        [StringLength(500, ErrorMessage = "نام فایل نباید بیشتر از 500 کاراکتر باشد")]
        public string? SourceFileName { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;
    }
}