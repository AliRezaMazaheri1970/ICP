namespace Application.Features.Reports.DTOs;

public class PivotRowDto
{
    public Guid SampleId { get; set; }
    public string SolutionLabel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Sample, Standard, Blank

    public double Weight { get; set; }
    public double Volume { get; set; }
    public double DilutionFactor { get; set; }

    // کلید: نام عنصر (مثلا "Cu 63")
    // مقدار: غلظت (یا هر پارامتر دیگری که گزارش می‌گیریم)
    // از string استفاده می‌کنیم تا بتوانیم مقادیر خاص مثل "N/A" یا "<LOD>" را هم بفرستیم
    public Dictionary<string, string> ElementValues { get; set; } = new();
}