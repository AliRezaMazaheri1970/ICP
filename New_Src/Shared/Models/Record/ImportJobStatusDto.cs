namespace Shared.Models;

public record ImportJobStatusDto(
    Guid JobId,
    ImportJobState State,
    int TotalRows,
    int ProcessedRows,
    string? Message,
    Guid? ProjectId,
    int Percent
);