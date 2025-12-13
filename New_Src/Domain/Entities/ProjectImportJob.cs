using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Represents a persistent job for importing or processing data.
/// </summary>
public class ProjectImportJob
{
    /// <summary>
    /// Gets or sets the unique identifier for the job.
    /// </summary>
    [Key]
    public Guid JobId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the project to process.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the resulting project after import.
    /// </summary>
    public Guid? ResultProjectId { get; set; }

    /// <summary>
    /// Gets or sets the user-provided name for the project.
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Gets or sets the type of job (e.g., "import" or "process").
    /// </summary>
    public string? JobType { get; set; }

    /// <summary>
    /// Gets or sets the current state of the job.
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// Gets or sets the total number of rows to process.
    /// </summary>
    public int TotalRows { get; set; }

    /// <summary>
    /// Gets or sets the number of rows already processed.
    /// </summary>
    public int ProcessedRows { get; set; }

    /// <summary>
    /// Gets or sets the completion percentage of the job.
    /// </summary>
    public int Percent { get; set; }

    /// <summary>
    /// Gets or sets a message describing the current status or error.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the path to the temporary uploaded file.
    /// </summary>
    public string? TempFilePath { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the job was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets an optional operation identifier for idempotency.
    /// </summary>
    public Guid? OperationId { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts made for this job.
    /// </summary>
    public int Attempts { get; set; } = 0;

    /// <summary>
    /// Gets or sets the last error message encountered, if any.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the scheduled time for the next attempt.
    /// </summary>
    public DateTime? NextAttemptAt { get; set; }
}