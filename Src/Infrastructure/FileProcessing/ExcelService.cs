using Application.Services.Interfaces;
using ClosedXML.Excel;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.FileProcessing;

public class ExcelService : IExcelService
{
    public async Task<List<Sample>> ReadSamplesFromExcelAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        var samples = new List<Sample>();

        try
        {
            // باز کردن فایل اکسل از روی رم (Stream)
            using var workbook = new XLWorkbook(fileStream);

            // گرفتن اولین شیت
            var worksheet = workbook.Worksheet(1);

            // بررسی محدوده داده‌ها (اگر شیت خالی باشد، نال برمی‌گرداند)
            var range = worksheet.RangeUsed();
            if (range == null)
            {
                return samples; // لیست خالی برمی‌گرداند
            }

            // ردیف اول را به عنوان هدر نگه می‌داریم تا نام عناصر را بخوانیم
            var headerRow = range.Row(1);

            // ردیف‌های داده (از ردیف دوم شروع می‌کنیم)
            var dataRows = range.RowsUsed().Skip(1);

            foreach (var row in dataRows)
            {
                // بررسی درخواست لغو عملیات
                cancellationToken.ThrowIfCancellationRequested();

                // 1. خواندن ستون‌های ثابت (ستون 1 تا 5)
                // A: Solution Label
                var label = row.Cell(1).GetValue<string>();

                // اگر لیبل خالی بود، این ردیف را نادیده می‌گیریم
                if (string.IsNullOrWhiteSpace(label)) continue;

                // B: Type
                var typeStr = row.Cell(2).GetValue<string>();
                if (!Enum.TryParse(typeStr, true, out SampleType type))
                {
                    type = SampleType.Sample; // مقدار پیش‌فرض
                }

                // C, D, E: Weight, Volume, DF (استفاده از TryGetValue برای امنیت بیشتر)
                // اگر سلول خالی یا متنی بود، مقادیر پیش‌فرض 0 یا 1 در نظر گرفته می‌شود
                var weight = row.Cell(3).TryGetValue(out double w) ? w : 0;
                var volume = row.Cell(4).TryGetValue(out double v) ? v : 0;
                var dilutionFactor = row.Cell(5).TryGetValue(out double df) ? df : 1;

                var sample = new Sample
                {
                    SolutionLabel = label,
                    Type = type,
                    Weight = weight,
                    Volume = volume,
                    DilutionFactor = dilutionFactor
                };

                // 2. خواندن ستون‌های داینامیک (عناصر) از ستون 6 تا آخر
                int colIndex = 6;
                int lastColumn = range.ColumnCount(); // آخرین ستونی که داده دارد

                while (colIndex <= lastColumn)
                {
                    // نام عنصر را از ردیف هدر می‌خوانیم (مثلا "Cu", "Zn")
                    var elementName = headerRow.Cell(colIndex).GetValue<string>();

                    // اگر هدر خالی بود، یعنی ستون معتبری نیست
                    if (string.IsNullOrWhiteSpace(elementName))
                    {
                        colIndex++;
                        continue;
                    }

                    // مقدار اندازه‌گیری شده را از ردیف جاری می‌خوانیم
                    var cell = row.Cell(colIndex);

                    // فقط اگر مقدار عددی بود اضافه می‌کنیم
                    if (cell.TryGetValue(out double measuredValue))
                    {
                        sample.Measurements.Add(new Measurement
                        {
                            ElementName = elementName,
                            Value = measuredValue
                        });
                    }

                    colIndex++;
                }

                samples.Add(sample);
            }
        }
        catch (Exception ex)
        {
            // در محیط واقعی بهتر است از ILogger استفاده کنید
            throw new Exception("Error processing Excel file.", ex);
        }

        // چون ClosedXML متد Async واقعی ندارد، خروجی را در Task می‌پیچیم
        return await Task.FromResult(samples);
    }
}