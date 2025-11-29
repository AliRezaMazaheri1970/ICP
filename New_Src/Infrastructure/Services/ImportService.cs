using Application.Services;
using Shared.Wrapper;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace Infrastructure.Services;

// Implementation uses CsvHelper to parse CSV and then uses the project persistence to save in chunks.
// Reports progress via IProgress<(total, processed)>.
public class ImportService : IImportService
{
    private readonly IProjectPersistenceService _persistence;
    private const int DefaultChunkSize = 100; // adjust as needed

    public ImportService(IProjectPersistenceService persistence)
    {
        _persistence = persistence;
    }

    public async Task<Result<ProjectSaveResult>> ImportCsvAsync(Stream csvStream, string projectName, string? owner = null, string? stateJson = null, IProgress<(int total, int processed)>? progress = null)
    {
        if (csvStream == null) return Result<ProjectSaveResult>.Fail("No stream provided");
        if (string.IsNullOrWhiteSpace(projectName)) projectName = "ImportedProject";

        // Ensure we have a seekable stream (counting requires seeking)
        MemoryStream ms;
        if (csvStream.CanSeek)
        {
            ms = csvStream as MemoryStream ?? new MemoryStream();
            if (!ReferenceEquals(ms, csvStream))
            {
                // copy contents into memory stream
                await csvStream.CopyToAsync(ms);
                ms.Position = 0;
            }
        }
        else
        {
            ms = new MemoryStream();
            await csvStream.CopyToAsync(ms);
            ms.Position = 0;
        }

        try
        {
            // First pass: count rows (excluding header)
            int totalRows = 0;
            using (var readerCount = new StreamReader(ms, leaveOpen: true))
            {
                var configCount = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    DetectDelimiter = true,
                    BadDataFound = null,
                    MissingFieldFound = null,
                    TrimOptions = TrimOptions.Trim
                };

                using var csvCount = new CsvReader(readerCount, configCount);
                if (!csvCount.Read()) return Result<ProjectSaveResult>.Fail("CSV empty");
                csvCount.ReadHeader();
                while (csvCount.Read())
                {
                    totalRows++;
                }
            }

            // Reset to start for actual processing
            ms.Position = 0;

            // Second pass: process in chunks and report progress
            using var reader = new StreamReader(ms);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                DetectDelimiter = true,
                BadDataFound = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim
            };

            using var csv = new CsvReader(reader, config);
            if (!csv.Read()) return Result<ProjectSaveResult>.Fail("CSV empty");
            csv.ReadHeader();
            var headers = csv.HeaderRecord;
            if (headers == null || headers.Length == 0) return Result<ProjectSaveResult>.Fail("CSV has no header");

            var processed = 0;
            Guid? knownProjectId = null;
            var batch = new List<RawDataDto>(DefaultChunkSize);

            while (csv.Read())
            {
                string? sampleId = null;
                var sampleIdHeader = headers.FirstOrDefault(h => string.Equals(h, "SampleId", StringComparison.OrdinalIgnoreCase));
                if (sampleIdHeader != null)
                {
                    sampleId = csv.GetField(sampleIdHeader);
                }

                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in headers)
                {
                    if (string.Equals(h, "SampleId", StringComparison.OrdinalIgnoreCase)) continue;
                    var value = csv.GetField(h);
                    if (value == null) { dict[h] = null; continue; }
                    if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) dict[h] = d;
                    else if (bool.TryParse(value, out var b)) dict[h] = b;
                    else dict[h] = value;
                }

                var columnDataJson = JsonSerializer.Serialize(dict);
                batch.Add(new RawDataDto(columnDataJson, string.IsNullOrWhiteSpace(sampleId) ? null : sampleId));
                processed++;

                // report intermediate progress if desired
                progress?.Report((totalRows, processed));

                if (batch.Count >= DefaultChunkSize)
                {
                    // save batch - if we already have project id use it, otherwise pass Guid.Empty
                    var saveProjectId = knownProjectId ?? Guid.Empty;
                    var res = await _persistence.SaveProjectAsync(saveProjectId, projectName, owner, batch, stateJson);
                    if (!res.Succeeded) return Result<ProjectSaveResult>.Fail($"Import failed during batch save: {res.Messages.FirstOrDefault()}");

                    knownProjectId = res.Data!.ProjectId;
                    batch.Clear();
                }
            }

            // save remaining
            if (batch.Count > 0)
            {
                var saveProjectId = knownProjectId ?? Guid.Empty;
                var res = await _persistence.SaveProjectAsync(saveProjectId, projectName, owner, batch, stateJson);
                if (!res.Succeeded) return Result<ProjectSaveResult>.Fail($"Import failed during final save: {res.Messages.FirstOrDefault()}");

                knownProjectId = res.Data!.ProjectId;
                batch.Clear();
            }

            // final report
            progress?.Report((totalRows, processed));

            return Result<ProjectSaveResult>.Success(new ProjectSaveResult(knownProjectId ?? Guid.Empty));
        }
        catch (Exception ex)
        {
            return Result<ProjectSaveResult>.Fail($"Import failed: {ex.Message}");
        }
        finally
        {
            try { ms.Dispose(); } catch { }
        }
    }
}