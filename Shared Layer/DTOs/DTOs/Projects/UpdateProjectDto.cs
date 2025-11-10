using System.ComponentModel.DataAnnotations;

namespace Shared.Icp.DTOs.Projects
{
    /// <summary>
    /// DTO برای ویرایش پروژه
    /// </summary>
    public class UpdateProjectDto
    {
        [Required(ErrorMessage = "شناسه الزامی است")]
        public int Id { get; set; }

        [Required(ErrorMessage = "نام پروژه الزامی است")]
        [StringLength(200, ErrorMessage = "نام پروژه نباید بیشتر از 200 کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "توضیحات نباید بیشتر از 1000 کاراکتر باشد")]
        public string? Description { get; set; }

        public DateTime? EndDate { get; set; }

        [Required(ErrorMessage = "وضعیت الزامی است")]
        [StringLength(50, ErrorMessage = "وضعیت نباید بیشتر از 50 کاراکتر باشد")]
        public string Status { get; set; } = "Active";
    }
}