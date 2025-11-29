using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Domain.Entities;

public class Project
{
    [Key]
    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    // Optional navigation collections to make relationships explicit
    public ICollection<RawDataRow> RawDataRows { get; set; } = new List<RawDataRow>();
    public ICollection<ProjectState> ProjectStates { get; set; } = new List<ProjectState>();
    public ICollection<ProcessedData> ProcessedDatas { get; set; } = new List<ProcessedData>();
}