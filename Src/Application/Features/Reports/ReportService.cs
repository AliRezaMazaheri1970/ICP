using Application.Features.Reports.DTOs;
using ClosedXML.Excel;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Interfaces.Services;

namespace Application.Services.Reports;

public class ReportService(IUnitOfWork unitOfWork) : IReportService
{
    public async Task<PivotReportDto> GetProjectPivotReportAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        // 1. دریافت نمونه‌ها به همراه اندازه‌گیری‌ها
        var sampleRepo = unitOfWork.Repository<Sample>();

        // استفاده از includeProperties برای بارگذاری Measurements
        var samples = await sampleRepo.GetAsync(
            s => s.ProjectId == projectId,
            includeProperties: "Measurements"
        );

        if (!samples.Any()) return new PivotReportDto();

        // 2. استخراج لیست تمام عناصر یکتا (ستون‌های جدول) و مرتب‌سازی الفبایی
        var allElements = samples
            .SelectMany(s => s.Measurements)
            .Select(m => m.ElementName)
            .Distinct()
            .OrderBy(e => e)
            .ToList();

        var report = new PivotReportDto
        {
            ElementHeaders = allElements
        };

        // 3. تبدیل داده‌ها به ساختار سطری (Pivoting)
        foreach (var sample in samples)
        {
            var row = new PivotRowDto
            {
                SampleId = sample.Id,
                SolutionLabel = sample.SolutionLabel,
                Type = sample.Type.ToString(),
                Weight = sample.Weight,
                Volume = sample.Volume,
                DilutionFactor = sample.DilutionFactor,
                ElementValues = new Dictionary<string, string>()
            };

            // پر کردن مقادیر عناصر برای این نمونه
            foreach (var element in allElements)
            {
                var measurement = sample.Measurements.FirstOrDefault(m => m.ElementName == element);

                if (measurement != null)
                {
                    // فرمت‌دهی مقدار تا 3 رقم اعشار (یا بر اساس نیاز پروژه)
                    row.ElementValues[element] = measurement.Value.ToString("F3");
                }
                else
                {
                    // اگر عنصری برای این نمونه اندازه گرفته نشده باشد
                    row.ElementValues[element] = "-";
                }
            }

            report.Rows.Add(row);
        }

        return report;
    }

    public async Task<byte[]> ExportProjectToExcelAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        // 1. دریافت داده‌های Pivot شده از متد بالا
        var pivotData = await GetProjectPivotReportAsync(projectId, cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Results");

        // 2. ایجاد هدرهای ثابت (ستون‌های 1 تا 5)
        worksheet.Cell(1, 1).Value = "Solution Label";
        worksheet.Cell(1, 2).Value = "Type";
        worksheet.Cell(1, 3).Value = "Weight";
        worksheet.Cell(1, 4).Value = "Volume";
        worksheet.Cell(1, 5).Value = "DF";

        // 3. ایجاد هدرهای داینامیک (عناصر) از ستون 6 به بعد
        int colIndex = 6;
        foreach (var element in pivotData.ElementHeaders)
        {
            worksheet.Cell(1, colIndex).Value = element;
            colIndex++;
        }

        // استایل دهی به ردیف هدر (بولد و رنگی)
        var headerRange = worksheet.Range(1, 1, 1, colIndex - 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 4. پر کردن داده‌های سطرها
        int rowIndex = 2;
        foreach (var row in pivotData.Rows)
        {
            worksheet.Cell(rowIndex, 1).Value = row.SolutionLabel;
            worksheet.Cell(rowIndex, 2).Value = row.Type;
            worksheet.Cell(rowIndex, 3).Value = row.Weight;
            worksheet.Cell(rowIndex, 4).Value = row.Volume;
            worksheet.Cell(rowIndex, 5).Value = row.DilutionFactor;

            // پر کردن مقادیر عناصر
            int elementColIndex = 6;
            foreach (var element in pivotData.ElementHeaders)
            {
                if (row.ElementValues.TryGetValue(element, out var val))
                {
                    // تلاش برای ذخیره به صورت عدد (برای محاسبات اکسل)
                    if (double.TryParse(val, out double numVal))
                    {
                        worksheet.Cell(rowIndex, elementColIndex).Value = numVal;
                    }
                    else
                    {
                        worksheet.Cell(rowIndex, elementColIndex).Value = val;
                    }
                }
                elementColIndex++;
            }
            rowIndex++;
        }

        // تنظیم عرض ستون‌ها به اندازه محتوا
        worksheet.Columns().AdjustToContents();

        // خروجی به صورت آرایه بایت
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}