namespace Application.Services;

/// <summary>
/// Defines services for Certified Reference Material (CRM) management and comparison.
/// </summary>
public interface ICrmService
{
    /// <summary>
    /// Retrieves a paginated list of CRM records with optional filtering.
    /// </summary>
    /// <param name="analysisMethod">Filter by analysis method.</param>
    /// <param name="searchText">Search text for CRM ID or Type.</param>
    /// <param name="ourOreasOnly">Filter for frequently used CRMs ("Our Oreas").</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <returns>A paginated list of CRMs.</returns>
    Task<Result<PaginatedResult<CrmListItemDto>>> GetCrmListAsync(
        string? analysisMethod = null,
        string? searchText = null,
        bool? ourOreasOnly = null,
        int page = 1,
        int pageSize = 50);

    /// <summary>
    /// Retrieves a specific CRM by its internal database ID.
    /// </summary>
    /// <param name="id">The CRM ID.</param>
    /// <returns>The CRM details.</returns>
    Task<Result<CrmListItemDto>> GetCrmByIdAsync(int id);

    /// <summary>
    /// Retrieves CRMs by their official identifier, optionally filtered by analysis method.
    /// </summary>
    /// <param name="crmId">The official CRM identifier (e.g., "OREAS 258").</param>
    /// <param name="analysisMethod">Optional analysis method.</param>
    /// <returns>A list of matching CRMs.</returns>
    Task<Result<List<CrmListItemDto>>> GetCrmByCrmIdAsync(string crmId, string? analysisMethod = null);

    /// <summary>
    /// Calculates the differences between project sample data and reference CRM values.
    /// </summary>
    /// <param name="request">The parameters specifying the comparison logic.</param>
    /// <returns>A list of difference results per sample.</returns>
    Task<Result<List<CrmDiffResultDto>>> CalculateDiffAsync(CrmDiffRequest request);

    /// <summary>
    /// Retrieves all available unique analysis methods from the CRM database.
    /// </summary>
    /// <returns>A list of analysis method names.</returns>
    Task<Result<List<string>>> GetAnalysisMethodsAsync();

    /// <summary>
    /// Adds a new CRM record or updates an existing one.
    /// </summary>
    /// <param name="request">The CRM data to upsert.</param>
    /// <returns>The ID of the upserted CRM.</returns>
    Task<Result<int>> UpsertCrmAsync(CrmUpsertRequest request);

    /// <summary>
    /// Deletes a CRM record by its ID.
    /// </summary>
    /// <param name="id">The ID of the CRM to delete.</param>
    /// <returns>True if deletion was successful; otherwise, false.</returns>
    Task<Result<bool>> DeleteCrmAsync(int id);

    /// <summary>
    /// Imports multiple CRM records from a CSV stream.
    /// </summary>
    /// <param name="csvStream">The stream containing CSV data.</param>
    /// <returns>The count of successfully imported records.</returns>
    Task<Result<int>> ImportCrmsFromCsvAsync(Stream csvStream);
}