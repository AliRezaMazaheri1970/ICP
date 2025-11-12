using Core.Icp.Domain.Base;
using Core.Icp.Domain.Entities.Elements;
using Core.Icp.Domain.Entities.Samples;
using Core.Icp.Domain.Enums;
using System.Text.Json;

namespace Core.Icp.Domain.Entities.Projects
{
    /// <summary>
    /// Represents an analysis project, which groups together samples, calibrations, and settings.
    /// </summary>
    public class Project : BaseEntity
    {
        /// <summary>
        /// Gets or sets the name of the project.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a detailed description of the project.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the name of the original source file (e.g., CSV, Excel) from which the project data was imported.
        /// </summary>
        public string? SourceFileName { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the project was started.
        /// </summary>
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the date and time when the project was completed.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Gets or sets the current status of the project (e.g., InProgress, Completed).
        /// </summary>
        public ProjectStatus Status { get; set; } = ProjectStatus.Created;

        /// <summary>
        /// Gets or sets project-specific settings, stored as a JSON string.
        /// </summary>
        public string? SettingsJson { get; set; }

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the collection of samples associated with this project.
        /// </summary>
        public virtual ICollection<Sample> Samples { get; set; } = new List<Sample>();

        /// <summary>
        /// Gets or sets the collection of calibration curves used within this project.
        /// </summary>
        public virtual ICollection<CalibrationCurve> CalibrationCurves { get; set; } = new List<CalibrationCurve>();

        #endregion

        #region Computed Properties

        /// <summary>
        /// Gets the total number of samples in this project.
        /// </summary>
        public int TotalSamples => Samples?.Count ?? 0;

        /// <summary>
        /// Gets the number of processed samples.
        /// </summary>
        public int ProcessedSamples => Samples?.Count(s => s.Status == SampleStatus.Processed) ?? 0;

        /// <summary>
        /// Gets the number of approved samples.
        /// </summary>
        public int ApprovedSamples => Samples?.Count(s => s.Status == SampleStatus.Approved) ?? 0;

        /// <summary>
        /// Gets the number of rejected samples.
        /// </summary>
        public int RejectedSamples => Samples?.Count(s => s.Status == SampleStatus.Rejected) ?? 0;

        /// <summary>
        /// Gets the number of pending samples.
        /// </summary>
        public int PendingSamples => Samples?.Count(s => s.Status == SampleStatus.Pending) ?? 0;

        /// <summary>
        /// Gets the progress percentage of the project (0-100).
        /// </summary>
        public double ProgressPercentage
        {
            get
            {
                if (TotalSamples == 0) return 0;
                return Math.Round((double)ProcessedSamples / TotalSamples * 100, 2);
            }
        }

        /// <summary>
        /// Gets the duration of the project in days.
        /// </summary>
        public int? DurationInDays
        {
            get
            {
                if (EndDate.HasValue)
                {
                    return (EndDate.Value - StartDate).Days;
                }
                return (DateTime.UtcNow - StartDate).Days;
            }
        }

        /// <summary>
        /// Gets whether the project is completed.
        /// </summary>
        public bool IsCompleted => Status == ProjectStatus.Approved || Status == ProjectStatus.Archived;

        /// <summary>
        /// Gets whether the project is active.
        /// </summary>
        public bool IsActive => Status == ProjectStatus.Processing || Status == ProjectStatus.UnderQualityCheck;

        #endregion

        #region Methods

        /// <summary>
        /// Adds a sample to the project.
        /// </summary>
        /// <param name="sample">The sample to add.</param>
        /// <exception cref="ArgumentNullException">Thrown if the sample is null.</exception>
        public void AddSample(Sample sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            sample.ProjectId = this.Id;
            sample.Project = this;

            if (Samples == null)
                Samples = new List<Sample>();

            Samples.Add(sample);
        }

        /// <summary>
        /// Adds multiple samples to the project.
        /// </summary>
        /// <param name="samples">The collection of samples to add.</param>
        /// <exception cref="ArgumentNullException">Thrown if the samples collection is null.</exception>
        public void AddSamples(IEnumerable<Sample> samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));

            foreach (var sample in samples)
            {
                AddSample(sample);
            }
        }

        /// <summary>
        /// Removes a sample from the project by its ID.
        /// </summary>
        /// <param name="sampleId">The ID of the sample to remove.</param>
        /// <returns><c>true</c> if the sample was successfully removed; otherwise, <c>false</c>.</returns>
        public bool RemoveSample(Guid sampleId)
        {
            var sample = Samples?.FirstOrDefault(s => s.Id == sampleId);
            if (sample != null)
            {
                return Samples!.Remove(sample);
            }
            return false;
        }

        /// <summary>
        /// Adds a calibration curve to the project.
        /// </summary>
        /// <param name="curve">The calibration curve to add.</param>
        /// <exception cref="ArgumentNullException">Thrown if the curve is null.</exception>
        public void AddCalibrationCurve(CalibrationCurve curve)
        {
            if (curve == null) throw new ArgumentNullException(nameof(curve));

            curve.ProjectId = this.Id;
            curve.Project = this;

            if (CalibrationCurves == null)
                CalibrationCurves = new List<CalibrationCurve>();

            CalibrationCurves.Add(curve);
        }

        /// <summary>
        /// Deserializes the <see cref="SettingsJson"/> string into a strongly-typed settings object.
        /// </summary>
        /// <typeparam name="T">The type of the settings object to deserialize to.</typeparam>
        /// <returns>The deserialized settings object, or <c>null</c> if deserialization fails or the JSON is empty.</returns>
        public T? GetSettings<T>() where T : class
        {
            if (string.IsNullOrWhiteSpace(SettingsJson))
                return null;

            try
            {
                return JsonSerializer.Deserialize<T>(SettingsJson);
            }
            catch
            {
                // Optionally log the deserialization error
                return null;
            }
        }

        /// <summary>
        /// Serializes a strongly-typed settings object into the <see cref="SettingsJson"/> string.
        /// </summary>
        /// <typeparam name="T">The type of the settings object.</typeparam>
        /// <param name="settings">The settings object to serialize.</param>
        public void SetSettings<T>(T settings) where T : class
        {
            if (settings == null)
            {
                SettingsJson = null;
                return;
            }

            SettingsJson = JsonSerializer.Serialize(settings);
        }

        /// <summary>
        /// Marks the project status as 'Processed' and sets the end date.
        /// </summary>
        public void Complete()
        {
            Status = ProjectStatus.Processed;
            EndDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks the project status as 'Approved' and sets the end date if not already set.
        /// </summary>
        public void Approve()
        {
            Status = ProjectStatus.Approved;
            if (!EndDate.HasValue)
            {
                EndDate = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Marks the project status as 'Rejected' and sets the end date if not already set.
        /// </summary>
        public void Reject()
        {
            Status = ProjectStatus.Rejected;
            if (!EndDate.HasValue)
            {
                EndDate = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Marks the project status as 'Archived' and sets the end date if not already set.
        /// </summary>
        public void Archive()
        {
            Status = ProjectStatus.Archived;
            if (!EndDate.HasValue)
            {
                EndDate = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Changes the project status to 'Processing' if it is currently 'Created' or 'Draft'.
        /// </summary>
        public void StartProcessing()
        {
            if (Status == ProjectStatus.Created || Status == ProjectStatus.Draft)
            {
                Status = ProjectStatus.Processing;
            }
        }

        /// <summary>
        /// Retrieves a collection of samples that match the specified status.
        /// </summary>
        /// <param name="status">The sample status to filter by.</param>
        /// <returns>An enumerable collection of samples with the given status.</returns>
        public IEnumerable<Sample> GetSamplesByStatus(SampleStatus status)
        {
            return Samples?.Where(s => s.Status == status) ?? Enumerable.Empty<Sample>();
        }

        /// <summary>
        /// Validates if the project is in a state where it can be processed.
        /// </summary>
        /// <returns><c>true</c> if the project status is 'Created' or 'Draft'; otherwise, <c>false</c>.</returns>
        public bool CanProcess()
        {
            return Status == ProjectStatus.Created || Status == ProjectStatus.Draft;
        }

        /// <summary>
        /// Validates if the project is in a state where it can be approved.
        /// </summary>
        /// <returns><c>true</c> if the project status is 'Processed' or 'UnderQualityCheck'; otherwise, <c>false</c>.</returns>
        public bool CanApprove()
        {
            return Status == ProjectStatus.Processed || Status == ProjectStatus.UnderQualityCheck;
        }

        #endregion
    }
}