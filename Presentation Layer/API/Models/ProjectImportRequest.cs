using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Presentation.Icp.API.Models
{
    /// <summary>
    /// مدل ورودی برای ایمپورت پروژه از فایل (multipart/form-data).
    /// </summary>
    public class ProjectImportRequest
    {
        /// <summary>
        /// فایل CSV یا Excel حاوی داده‌ها.
        /// </summary>
        [Required(ErrorMessage = "لطفاً فایل داده را ارسال کنید.")]
        public IFormFile File { get; set; } = default!;

        /// <summary>
        /// نام پروژه‌ای که باید ساخته شود.
        /// </summary>
        [Required(ErrorMessage = "وارد کردن نام پروژه الزامی است.")]
        [MaxLength(200, ErrorMessage = "حداکثر طول نام پروژه ۲۰۰ کاراکتر است.")]
        public string ProjectName { get; set; } = string.Empty;
    }
}
