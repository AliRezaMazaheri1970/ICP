using Application.Services.Interfaces;
using ClosedXML.Excel; // <--- استفاده از پکیج نصب شده
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.FileProcessing;

public class ExcelService : IExcelService
{
    public async Task<List<Sample>> ReadSamplesFromExcelAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        var samples = new List<Sample>();

        // باز کردن فایل اکسل از روی رم (Stream)
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheet(1); // شیت اول

        // فرض می‌کنیم ردیف اول هدر است
        var rows = worksheet.RangeUsed().RowsUsed().Skip(1);

        foreach (var row in rows)
        {
            // خواندن سلول‌ها
            var label = row.Cell(1).GetValue<string>();
            var typeStr = row.Cell(2).GetValue<string>();

            // تبدیل Enum
            if (!Enum.TryParse(typeStr, true, out SampleType type))
                type = SampleType.Sample;

            var sample = new Sample
            {
                SolutionLabel = label,
                Type = type,
                Weight = row.Cell(3).GetValue<double>(),
                Volume = row.Cell(4).GetValue<double>(),
                DilutionFactor = row.Cell(5).GetValue<double>()
            };

            // خواندن ستون‌های داینامیک (عناصر) از ستون 6 به بعد
            int colIndex = 6;
            while (!row.Cell(colIndex).IsEmpty())
            {
                var elementName = worksheet.Row(1).Cell(colIndex).GetValue<string>();
                var value = row.Cell(colIndex).GetValue<double>();

                sample.Measurements.Add(new Measurement
                {
                    ElementName = elementName,
                    Value = value
                });

                colIndex++;
            }

            samples.Add(sample);
        }

        // چون ClosedXML عملیات Async واقعی ندارد، نتیجه را در یک Task برمی‌گردانیم
        return await Task.FromResult(samples);
    }
}