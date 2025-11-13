using Core.Icp.Domain.Enums;
using Infrastructure.Icp.Reports.Models;
using Shared.Icp.DTOs.Projects;
using Shared.Icp.DTOs.QualityControl;
using Shared.Icp.DTOs.Reports;
using Shared.Icp.DTOs.Samples;
using System.Runtime.Serialization;

namespace Infrastructure.Icp.Reports.Interfaces;

public interface IReportService
{
    // ==================== گزارش‌های Sample ====================

    /// <summary>
    /// گزارش کامل یک نمونه
    /// </summary>
    Task<ReportResultDto> GenerateSampleReportAsync(
        Guid sampleId,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// گزارش دسته‌ای چندین نمونه
    /// </summary>
    Task<ReportResultDto> GenerateBatchSampleReportAsync(
        IEnumerable<Guid> sampleIds,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    // ==================== گزارش‌های Project ====================

    /// <summary>
    /// گزارش خلاصه پروژه
    /// </summary>
    Task<ReportResultDto> GenerateProjectSummaryReportAsync(
        Guid projectId,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// گزارش کامل پروژه با تمام جزئیات
    /// </summary>
    Task<ReportResultDto> GenerateCompleteProjectReportAsync(
        Guid projectId,
        ReportFormat format,
        ReportOptions options,
        CancellationToken cancellationToken = default);

    // ==================== گزارش‌های Quality Control ====================

    /// <summary>
    /// گزارش کنترل کیفیت
    /// </summary>
    Task<ReportResultDto> GenerateQualityControlReportAsync(
        QualityCheckSummaryDto qualityData,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// گزارش نمونه‌های حذف شده
    /// </summary>
    Task<ReportResultDto> GenerateRejectedSamplesReportAsync(
        Guid projectId,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    // ==================== گزارش‌های CRM ====================

    /// <summary>
    /// گزارش تحلیل CRM
    /// </summary>
    Task<ReportResultDto> GenerateCrmAnalysisReportAsync(
        Guid crmId,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// گزارش Blank & Scale Correction
    /// </summary>
    Task<ReportResultDto> GenerateBlankScaleCorrectionReportAsync(
        Guid projectId,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    // ==================== گزارش‌های Drift ====================

    /// <summary>
    /// گزارش Drift Correction
    /// </summary>
    Task<ReportResultDto> GenerateDriftCorrectionReportAsync(
        Guid projectId,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    // ==================== گزارش‌های Calibration ====================

    /// <summary>
    /// گزارش منحنی کالیبراسیون
    /// </summary>
    Task<ReportResultDto> GenerateCalibrationCurveReportAsync(
        Guid curveId,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    // ==================== گزارش‌های آماری ====================

    /// <summary>
    /// گزارش آماری عناصر
    /// </summary>
    Task<ReportResultDto> GenerateElementStatisticsReportAsync(
        Guid projectId,
        IEnumerable<string> elementSymbols,
        ReportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// گزارش مقایسه‌ای نمونه‌ها
    /// </summary>
    Task<ReportResultDto> GenerateComparisonReportAsync(
        IEnumerable<Guid> sampleIds,
        ReportFormat format,
        ComparisonOptions options,
        CancellationToken cancellationToken = default);

    // ==================== متدهای کمکی ====================

    /// <summary>
    /// بررسی در دسترس بودن Template
    /// </summary>
    Task<bool> IsTemplateAvailableAsync(string templateName);

    /// <summary>
    /// لیست تمام Template های موجود
    /// </summary>
    Task<IEnumerable<TemplateInfoDto>> GetAvailableTemplatesAsync();

    /// <summary>
    /// ذخیره گزارش در دیتابیس
    /// </summary>
    Task<Guid> SaveReportAsync(ReportResultDto report, CancellationToken cancellationToken = default);

    /// <summary>
    /// دریافت گزارش ذخیره شده
    /// </summary>
    Task<ReportResultDto> GetSavedReportAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// دریافت تاریخچه گزارش‌ها
    /// </summary>
    Task<List<ReportHistoryDto>> GetReportHistoryAsync(
        Guid? projectId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// حذف گزارش
    /// </summary>
    Task<bool> DeleteReportAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// دانلود گزارش ذخیره شده
    /// </summary>
    Task<byte[]> DownloadReportAsync(Guid reportId, CancellationToken cancellationToken = default);
}