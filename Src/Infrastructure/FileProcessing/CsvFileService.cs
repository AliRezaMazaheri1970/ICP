using System.Text.RegularExpressions;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.FileProcessing;

public class CsvFileService : IFileImportService
{
    public bool CanSupport(string fileName)
    {
        return fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<Sample>> ProcessFileAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        var samples = new List<Sample>();
        using var reader = new StreamReader(fileStream);

        var lines = new List<string>();

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        if (lines.Count == 0) return samples;

        // ادامه منطق بدون تغییر...
        bool isNewFormat = lines.Take(20).Any(l => l.Contains("Sample ID:", StringComparison.OrdinalIgnoreCase));

        if (isNewFormat)
            ParseNewFormat(lines, samples);
        else
            ParseTabularFormat(lines, samples);

        return samples;
    }

    private void ParseNewFormat(List<string> lines, List<Sample> samples)
    {
        Sample? currentSample = null;

        foreach (var line in lines)
        {
            var parts = line.Split(',');

            if (parts.Length > 0 && parts[0].StartsWith("Sample ID:", StringComparison.OrdinalIgnoreCase))
            {
                var rawId = parts[0].Split(':')[1].Trim();
                if (string.IsNullOrEmpty(rawId)) rawId = "Unknown_Sample";

                currentSample = new Sample
                {
                    Id = Guid.NewGuid(),
                    SolutionLabel = rawId,
                    Type = DetectSampleType(rawId),
                    Measurements = new List<Measurement>()
                };
                samples.Add(currentSample);
                continue;
            }

            if (currentSample != null && parts.Length >= 2)
            {
                var rawElement = parts[0].Trim();
                if (rawElement.StartsWith("Method File") || rawElement.StartsWith("Calibration File")) continue;

                if (IsElement(rawElement, out string cleanName))
                {
                    if (double.TryParse(parts[1], out double intensity))
                    {
                        currentSample.Measurements.Add(new Measurement
                        {
                            Id = Guid.NewGuid(),
                            ElementName = cleanName,
                            Value = intensity,
                            Unit = "cps"
                        });
                    }
                }
            }
        }
    }

    private void ParseTabularFormat(List<string> lines, List<Sample> samples)
    {
        if (lines.Count < 2) return;

        var headers = lines[0].Split(',').Select(h => h.Trim().ToLower()).ToList();
        int sampleIdIndex = headers.IndexOf("sample id");
        if (sampleIdIndex == -1) sampleIdIndex = headers.IndexOf("solution label");

        if (sampleIdIndex == -1) return;

        var elementColIndices = new Dictionary<int, string>();
        for (int i = 0; i < headers.Count; i++)
        {
            if (IsElement(headers[i], out string cleanName))
                elementColIndices[i] = cleanName;
        }

        for (int i = 1; i < lines.Count; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length <= sampleIdIndex) continue;

            var label = parts[sampleIdIndex].Trim();
            if (string.IsNullOrEmpty(label)) continue;

            var sample = new Sample
            {
                Id = Guid.NewGuid(),
                SolutionLabel = label,
                Type = DetectSampleType(label),
                Measurements = new List<Measurement>()
            };

            foreach (var kvp in elementColIndices)
            {
                if (parts.Length > kvp.Key && double.TryParse(parts[kvp.Key], out double val))
                {
                    sample.Measurements.Add(new Measurement
                    {
                        Id = Guid.NewGuid(),
                        ElementName = kvp.Value,
                        Value = val,
                        Unit = "ppm"
                    });
                }
            }
            samples.Add(sample);
        }
    }

    private bool IsElement(string input, out string cleanName)
    {
        var match = Regex.Match(input.Trim(), @"^([A-Za-z]+)[\s-]?(\d+\.?\d*)$");
        if (match.Success)
        {
            cleanName = $"{match.Groups[1].Value} {match.Groups[2].Value}";
            return true;
        }
        if (Regex.IsMatch(input.Trim(), @"^[A-Z][a-z]?$"))
        {
            cleanName = input.Trim();
            return true;
        }
        cleanName = string.Empty;
        return false;
    }

    private SampleType DetectSampleType(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return SampleType.Unknown;
        var upper = label.ToUpper();
        if (upper.Contains("STD")) return SampleType.Standard;
        if (upper.Contains("BLK") || upper.Contains("BLANK")) return SampleType.Blank;
        return SampleType.Sample;
    }
}