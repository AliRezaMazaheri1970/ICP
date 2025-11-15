using System;
using System.Collections.Generic;

namespace Presentation.Icp.API.Models
{
    /// <summary>
    /// نتیجه سطح بالا برای ایمپورت فایل و ایجاد پروژه.
    /// </summary>
    public class FileImportResultDto
    {
        /// <summary>
        /// شناسه پروژه ساخته‌شده. اگر پروژه به‌کلی ساخته نشده باشد مقدار null است.
        /// </summary>
        public Guid? ProjectId { get; set; }

        /// <summary>
        /// نام پروژه ساخته‌شده (در صورت موفقیت).
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// وضعیت کلی عملیات ایمپورت (صرف‌نظر از خطاهای ردیفی).
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// پیام کلی عملیات (موفقیت/خطا).
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// تعداد کل رکوردهای خوانده‌شده از فایل.
        /// </summary>
        public int TotalRecords { get; set; }

        /// <summary>
        /// تعداد رکوردهایی که با موفقیت ذخیره شده‌اند.
        /// </summary>
        public int SuccessfulRecords { get; set; }

        /// <summary>
        /// تعداد رکوردهایی که در آن‌ها خطا رخ داده است.
        /// </summary>
        public int FailedRecords { get; set; }

        /// <summary>
        /// تعداد نمونه‌های (Samples) ایجادشده در پروژه.
        /// </summary>
        public int TotalSamples { get; set; }

        /// <summary>
        /// لیست خطاهای کلی یا مرتبط با رکوردها (برای نمایش به کاربر).
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// لیست هشدارها (مثلاً رکوردهایی که اسکپ شده‌اند ولی خطا حیاتی نبوده).
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        #region Factory Methods

        public static FileImportResultDto CreateSuccess(
            Guid projectId,
            string projectName,
            int totalRecords,
            int successfulRecords,
            int failedRecords,
            int totalSamples,
            List<string>? warnings = null)
        {
            return new FileImportResultDto
            {
                ProjectId = projectId,
                ProjectName = projectName,
                Success = true,
                Message = "ایمپورت فایل با موفقیت انجام شد.",
                TotalRecords = totalRecords,
                SuccessfulRecords = successfulRecords,
                FailedRecords = failedRecords,
                TotalSamples = totalSamples,
                Warnings = warnings ?? new List<string>()
            };
        }

        public static FileImportResultDto CreateFailure(
            string message,
            List<string>? errors = null,
            List<string>? warnings = null)
        {
            return new FileImportResultDto
            {
                ProjectId = null,
                ProjectName = string.Empty,
                Success = false,
                Message = message,
                TotalRecords = 0,
                SuccessfulRecords = 0,
                FailedRecords = 0,
                TotalSamples = 0,
                Errors = errors ?? new List<string>(),
                Warnings = warnings ?? new List<string>()
            };
        }

        #endregion
    }
}
