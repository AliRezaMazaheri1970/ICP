namespace Shared.Icp.DTOs.Samples
{
    /// <summary>
    /// DTO used to update an existing sample in the system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Intended for update scenarios (PUT/PATCH) in the API layer to receive only the mutable fields of a sample
    /// from the client and persist them to the data store. <see cref="SampleName"/> is required; other fields are optional
    /// so you can perform partial updates. When a field is not provided (null), the server may ignore it or keep the current value
    /// according to the domain rules.
    /// </para>
    /// <para>
    /// Units are specified in each field's documentation. Validation (e.g., non-negative weight/volume and dilution factor greater than zero)
    /// should be enforced by the domain layer or the controller; this DTO is a simple data carrier.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example usage for updating an existing sample
    /// var update = new UpdateSampleDto
    /// {
    ///     SampleName = "Sample A-123",
    ///     RunDate = DateTime.UtcNow,
    ///     Weight = 1.250m,       // grams
    ///     Volume = 50.0m,        // milliliters
    ///     DilutionFactor = 10m,  // unitless
    ///     Status = "Completed",
    ///     Notes = "Results verified"
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="CreateSampleDto"/>
    /// <seealso cref="SampleDto"/>
    public class UpdateSampleDto
    {
        /// <summary>
        /// Human-readable name of the sample.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Used for identifying the sample in reports and results. It is recommended to keep it unique, or combine it with a
        /// project/category identifier to avoid ambiguity.
        /// </para>
        /// </remarks>
        /// <value>
        /// Non-empty string. Empty values may be rejected by the domain layer.
        /// </value>
        public string SampleName { get; set; } = string.Empty;

        /// <summary>
        /// Test execution date/time (optional).
        /// </summary>
        /// <remarks>
        /// <para>
        /// When provided, prefer UTC to avoid time zone inconsistencies. If omitted, the previous run date remains unchanged.
        /// </para>
        /// </remarks>
        /// <value>
        /// Date/time value or null.
        /// </value>
        public DateTime? RunDate { get; set; }

        /// <summary>
        /// Sample weight in grams (optional).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Must be non-negative and, depending on domain rules, greater than zero. For very small values use decimal precision in grams.
        /// </para>
        /// </remarks>
        /// <value>
        /// Decimal or null. Unit: grams (g).
        /// </value>
        public decimal? Weight { get; set; }

        /// <summary>
        /// Sample volume in milliliters (optional).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Must be non-negative and, depending on domain rules, greater than zero. Preserve precision if converting units.
        /// </para>
        /// </remarks>
        /// <value>
        /// Decimal or null. Unit: milliliters (mL).
        /// </value>
        public decimal? Volume { get; set; }

        /// <summary>
        /// Dilution factor (optional).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unitless number that typically must be greater than zero. Use 1 for no dilution.
        /// </para>
        /// </remarks>
        /// <value>
        /// Decimal or null. Unitless.
        /// </value>
        public decimal? DilutionFactor { get; set; }

        /// <summary>
        /// Sample status (optional).
        /// </summary>
        /// <remarks>
        /// <para>
        /// One of the valid business/domain values (e.g., "Pending", "InProgress", "Completed", "Rejected").
        /// When not provided, the current status is left unchanged.
        /// </para>
        /// </remarks>
        /// <value>
        /// String or null.
        /// </value>
        public string? Status { get; set; }

        /// <summary>
        /// Free-form notes and additional details (optional).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use for contextual information, QC notes, rejection reasons, or any other relevant explanation.
        /// </para>
        /// </remarks>
        /// <value>
        /// String or null.
        /// </value>
        public string? Notes { get; set; }
    }
}