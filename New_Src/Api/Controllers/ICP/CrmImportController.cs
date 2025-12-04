using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace Api.Controllers.ICP;

[ApiController]
[Route("api/crm")]
public class CrmImportController : ControllerBase
{
    private readonly IsatisDbContext _db;
    private readonly ILogger<CrmImportController> _logger;

    public CrmImportController(IsatisDbContext db, ILogger<CrmImportController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Import CRM data from CSV file
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportCrmFromCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var headerLine = await reader.ReadLineAsync();
            
            if (string.IsNullOrEmpty(headerLine))
                return BadRequest("Empty file");

            // Parse headers - first column is index, second is CRM ID, third is Analysis Method
            var headers = ParseCsvLine(headerLine);
            var elementColumns = headers.Skip(3).ToList(); // Skip index, CRM ID, Analysis Method

            var crmRecords = new List<CrmData>();
            var lineNumber = 1;
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = ParseCsvLine(line);
                
                if (values.Count < 3)
                    continue;

                var crmId = values[1]?.Trim() ?? "";
                var analysisMethod = values[2]?.Trim() ?? "";

                if (string.IsNullOrEmpty(crmId))
                    continue;

                // Determine type: OREAS if starts with "OREAS", otherwise CRM
                var type = crmId.StartsWith("OREAS", StringComparison.OrdinalIgnoreCase) ? "OREAS" : "CRM";

                // Build element values dictionary
                var elementValues = new Dictionary<string, double>();
                for (int i = 3; i < values.Count && i - 3 < elementColumns.Count; i++)
                {
                    var elementName = elementColumns[i - 3];
                    if (double.TryParse(values[i], NumberStyles.Any, CultureInfo.InvariantCulture, out var value) && value != 0)
                    {
                        elementValues[elementName] = value;
                    }
                }

                var crmData = new CrmData
                {
                    CrmId = crmId,
                    AnalysisMethod = analysisMethod,
                    Type = type,
                    ElementValues = JsonSerializer.Serialize(elementValues),
                    IsOurOreas = type == "OREAS",
                    CreatedAt = DateTime.UtcNow
                };

                crmRecords.Add(crmData);
            }

            // Clear existing data
            await _db.CrmData.ExecuteDeleteAsync();

            // Insert new data
            await _db.CrmData.AddRangeAsync(crmRecords);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Imported {Count} CRM records from CSV", crmRecords.Count);

            return Ok(new
            {
                success = true,
                imported = crmRecords.Count,
                oreasCount = crmRecords.Count(c => c.Type == "OREAS"),
                crmCount = crmRecords.Count(c => c.Type == "CRM"),
                elements = elementColumns.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import CRM data");
            return StatusCode(500, $"Import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Import CRM data from file path on server
    /// </summary>
    [HttpPost("import-from-path")]
    public async Task<IActionResult> ImportCrmFromPath([FromBody] ImportFromPathRequest request)
    {
        if (string.IsNullOrEmpty(request?.FilePath))
            return BadRequest("File path is required");

        if (!System.IO.File.Exists(request.FilePath))
            return NotFound($"File not found: {request.FilePath}");

        try
        {
            using var reader = new StreamReader(request.FilePath);
            var headerLine = await reader.ReadLineAsync();
            
            if (string.IsNullOrEmpty(headerLine))
                return BadRequest("Empty file");

            var headers = ParseCsvLine(headerLine);
            var elementColumns = headers.Skip(3).ToList();

            var crmRecords = new List<CrmData>();
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = ParseCsvLine(line);
                
                if (values.Count < 3)
                    continue;

                var crmId = values[1]?.Trim() ?? "";
                var analysisMethod = values[2]?.Trim() ?? "";

                if (string.IsNullOrEmpty(crmId))
                    continue;

                var type = crmId.StartsWith("OREAS", StringComparison.OrdinalIgnoreCase) ? "OREAS" : "CRM";

                var elementValues = new Dictionary<string, double>();
                for (int i = 3; i < values.Count && i - 3 < elementColumns.Count; i++)
                {
                    var elementName = elementColumns[i - 3];
                    if (double.TryParse(values[i], NumberStyles.Any, CultureInfo.InvariantCulture, out var value) && value != 0)
                    {
                        elementValues[elementName] = value;
                    }
                }

                var crmData = new CrmData
                {
                    CrmId = crmId,
                    AnalysisMethod = analysisMethod,
                    Type = type,
                    ElementValues = JsonSerializer.Serialize(elementValues),
                    IsOurOreas = type == "OREAS",
                    CreatedAt = DateTime.UtcNow
                };

                crmRecords.Add(crmData);
            }

            // Clear existing and insert new
            await _db.CrmData.ExecuteDeleteAsync();
            await _db.CrmData.AddRangeAsync(crmRecords);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Imported {Count} CRM records from {Path}", crmRecords.Count, request.FilePath);

            return Ok(new
            {
                success = true,
                imported = crmRecords.Count,
                oreasCount = crmRecords.Count(c => c.Type == "OREAS"),
                crmCount = crmRecords.Count(c => c.Type == "CRM"),
                elements = elementColumns.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import CRM data from {Path}", request.FilePath);
            return StatusCode(500, $"Import failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all CRM data (for import verification)
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? type = null)
    {
        var query = _db.CrmData.AsQueryable();
        
        if (!string.IsNullOrEmpty(type))
            query = query.Where(c => c.Type == type);

        var data = await query.OrderBy(c => c.CrmId).ToListAsync();
        
        return Ok(new
        {
            success = true,
            count = data.Count,
            data = data.Select(c => new
            {
                c.Id,
                c.CrmId,
                c.AnalysisMethod,
                c.Type,
                c.IsOurOreas,
                elements = JsonSerializer.Deserialize<Dictionary<string, double>>(c.ElementValues)
            })
        });
    }

    /// <summary>
    /// Get CRM statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var total = await _db.CrmData.CountAsync();
        var oreasCount = await _db.CrmData.CountAsync(c => c.Type == "OREAS");
        var crmCount = await _db.CrmData.CountAsync(c => c.Type == "CRM");
        var methodsCount = await _db.CrmData.Select(c => c.AnalysisMethod).Distinct().CountAsync();

        return Ok(new
        {
            total,
            oreasCount,
            crmCount,
            methodsCount
        });
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var currentValue = "";

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentValue);
                currentValue = "";
            }
            else
            {
                currentValue += c;
            }
        }
        result.Add(currentValue);

        return result;
    }

    public class ImportFromPathRequest
    {
        public string? FilePath { get; set; }
    }
}
