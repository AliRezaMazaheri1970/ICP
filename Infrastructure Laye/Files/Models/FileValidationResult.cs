namespace Infrastructure.Icp.Files.Models
{
    /// <summary>
    /// نتیجه اعتبارسنجی فایل
    /// </summary>
    public class FileValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int InvalidRows { get; set; }
        public Dictionary<string, List<string>> RowErrors { get; set; } = new();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public void AddRowError(int rowNumber, string error)
        {
            var key = $"Row {rowNumber}";
            if (!RowErrors.ContainsKey(key))
            {
                RowErrors[key] = new List<string>();
            }
            RowErrors[key].Add(error);
            IsValid = false;
        }
    }
}