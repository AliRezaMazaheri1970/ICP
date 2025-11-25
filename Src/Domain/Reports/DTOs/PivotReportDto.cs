namespace Application.Features.Reports.DTOs;

public class PivotReportDto
{
    // لیست تمام ستون‌های عنصر (برای ساخت هدر جدول در UI/Excel)
    // مثلا: ["Li 7", "Cu 63", "Zn 66", ...]
    public List<string> ElementHeaders { get; set; } = new();

    // داده‌های سطر به سطر
    public List<PivotRowDto> Rows { get; set; } = new();
}