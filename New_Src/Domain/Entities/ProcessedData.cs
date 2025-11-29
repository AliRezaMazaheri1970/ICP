using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

// Small entity to record processed/analysis outputs (optional)
public class ProcessedData
{
    // Primary key
    [Key]
    public int Id { get; set; }

    // A user-defined or internally incremented id for this processing run/type within a project
    // (optional, can be used to group multiple processed records per analysis)
    public int ProcessedId { get; set; }

    // FK to Project
    public Guid ProjectId { get; set; }

    // e.g. "CRM", "RM", "QC" - keep short
    public string AnalysisType { get; set; } = string.Empty;

    // JSON payload of processed results (use nvarchar(max) mapping in EF config)
    public string Data { get; set; } = string.Empty;

    // Created timestamp (set by CLR default; DB default also configured in EF config)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Optional navigation to Project (no assumption made that Project has a collection property)
    public Project? Project { get; set; }
}