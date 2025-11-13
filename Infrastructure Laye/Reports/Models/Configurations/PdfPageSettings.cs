using Core.Icp.Domain.Enums;
using Shared.Icp.DTOs.Reports;

namespace Infrastructure.Icp.Reports.Models.Configurations;

/// <summary>
/// تنظیمات صفحه PDF
/// </summary>
public class PdfPageSettings
{
    /// <summary>
    /// اندازه صفحه
    /// </summary>
    public PageSize PageSize { get; set; } = PageSize.A4;

    /// <summary>
    /// جهت صفحه
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>
    /// حاشیه‌های صفحه
    /// </summary>
    public PageMargins Margins { get; set; } = new();

    /// <summary>
    /// متن هدر
    /// </summary>
    public string? HeaderText { get; set; }

    /// <summary>
    /// متن فوتر
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>
    /// نمایش شماره صفحه
    /// </summary>
    public bool ShowPageNumbers { get; set; } = true;

    /// <summary>
    /// محل نمایش شماره صفحه
    /// </summary>
    public PageNumberPosition PageNumberPosition { get; set; } = PageNumberPosition.BottomCenter;

    /// <summary>
    /// فونت پیش‌فرض
    /// </summary>
    public string DefaultFontFamily { get; set; } = "B Nazanin";

    /// <summary>
    /// اندازه فونت پیش‌فرض
    /// </summary>
    public int DefaultFontSize { get; set; } = 12;

    /// <summary>
    /// رنگ پیش‌زمینه
    /// </summary>
    public string ForegroundColor { get; set; } = "#000000";

    /// <summary>
    /// رنگ پس‌زمینه
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// فاصله خطوط (Line Height)
    /// </summary>
    public double LineHeight { get; set; } = 1.5;

    /// <summary>
    /// جهت متن (RTL/LTR)
    /// </summary>
    public TextDirection TextDirection { get; set; } = TextDirection.RightToLeft;

    /// <summary>
    /// کیفیت تصاویر (0-100)
    /// </summary>
    public int ImageQuality { get; set; } = 85;

    /// <summary>
    /// فشرده‌سازی PDF
    /// </summary>
    public bool CompressPdf { get; set; } = true;
}

/// <summary>
/// محل نمایش شماره صفحه
/// </summary>
public enum PageNumberPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

/// <summary>
/// جهت متن
/// </summary>
public enum TextDirection
{
    LeftToRight,
    RightToLeft
}
