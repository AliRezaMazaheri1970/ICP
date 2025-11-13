namespace Infrastructure.Icp.Files.Models
{
    /// <summary>
    /// تعریف ستون‌های فایل ورودی
    /// </summary>
    public class FileColumn
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public int Index { get; set; }
        public string? Alias { get; set; }

        public static List<FileColumn> GetICPMSColumns()
        {
            return new List<FileColumn>
            {
                new() { Name = "SampleId", DataType = "string", IsRequired = true, Index = 0 },
                new() { Name = "SampleName", DataType = "string", IsRequired = true, Index = 1 },
                new() { Name = "RunDate", DataType = "datetime", IsRequired = true, Index = 2 },
                new() { Name = "Weight", DataType = "decimal", IsRequired = true, Index = 3 },
                new() { Name = "Volume", DataType = "decimal", IsRequired = true, Index = 4 },
                new() { Name = "DilutionFactor", DataType = "int", IsRequired = true, Index = 5 }
            };
        }
    }
}