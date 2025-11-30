using Application.DTOs;
using Shared.Wrapper;

namespace Application.Services;

/// <summary>
/// Service for CRM (Certified Reference Material) operations. 
/// Equivalent to CRM.py and crm_manager. py in Python code.
/// </summary>
public interface ICrmService
{
    /// <summary>
    /// Get list of all CRMs with optional filtering
    /// </summary>
    Task<Result<List<CrmListItemDto>>> GetCrmListAsync(
        string? analysisMethod = null,
        string? searchText = null,
        bool? ourOreasOnly = null,
        int page = 1,
        int pageSize = 50);

    /// <summary>
    /// Get a single CRM by its ID
    /// </summary>
    Task<Result<CrmListItemDto>> GetCrmByIdAsync(int id);

    /// <summary>
    /// Get a CRM by its CRM ID string (e.g., "OREAS 258")
    /// </summary>
    Task<Result<CrmListItemDto>> GetCrmByCrmIdAsync(string crmId, string? analysisMethod = null);

    /// <summary>
    /// Calculate differences between project data and CRM values. 
    /// This is the core function matching crm_manager.py's check_rm and _build_crm_row_lists_for_columns. 
    /// </summary>
    Task<Result<List<CrmDiffResultDto>>> CalculateDiffAsync(CrmDiffRequest request);

    /// <summary>
    /// Get available analysis methods
    /// </summary>
    Task<Result<List<string>>> GetAnalysisMethodsAsync();

    /// <summary>
    /// Add or update a CRM record
    /// </summary>
    Task<Result<int>> UpsertCrmAsync(CrmUpsertRequest request);

    /// <summary>
    /// Delete a CRM record
    /// </summary>
    Task<Result<bool>> DeleteCrmAsync(int id);

    /// <summary>
    /// Import CRMs from a CSV or JSON source (bulk)
    /// </summary>
    Task<Result<int>> ImportCrmsAsync(Stream dataStream, string format = "csv");
}