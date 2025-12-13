using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs; 
namespace WebUI.Services;

// ============================================
// Correction DTOs
// ============================================

public class BadSampleDto
{
    [JsonPropertyName("solutionLabel")]
    public string SolutionLabel { get; set; } = "";

    [JsonPropertyName("actualValue")]
    public decimal ActualValue { get; set; }

    [JsonPropertyName("corrCon")]
    public decimal CorrCon { get; set; }

    [JsonPropertyName("expectedValue")]
    public decimal ExpectedValue { get; set; }

    [JsonPropertyName("deviation")]
    public decimal Deviation { get; set; }
}

public class EmptyRowDto
{
    [JsonPropertyName("solutionLabel")]
    public string SolutionLabel { get; set; } = "";

    [JsonPropertyName("elementValues")]
    public Dictionary<string, decimal?> ElementValues { get; set; } = new();

    [JsonPropertyName("elementAverages")]
    public Dictionary<string, decimal> ElementAverages { get; set; } = new();

    [JsonPropertyName("percentOfAverage")]
    public Dictionary<string, decimal> PercentOfAverage { get; set; } = new();

    [JsonPropertyName("elementsBelowThreshold")]
    public int ElementsBelowThreshold { get; set; }

    [JsonPropertyName("totalElementsChecked")]
    public int TotalElementsChecked { get; set; }

    [JsonPropertyName("overallScore")]
    public decimal OverallScore { get; set; }
}

public class CorrectionResultDto
{
    [JsonPropertyName("totalRows")]
    public int TotalRows { get; set; }

    [JsonPropertyName("correctedRows")]
    public int CorrectedRows { get; set; }

    [JsonPropertyName("correctedSamples")]
    public List<CorrectedSampleInfo> CorrectedSamples { get; set; } = new();
}

public class CorrectedSampleInfo
{
    [JsonPropertyName("solutionLabel")]
    public string SolutionLabel { get; set; } = "";

    [JsonPropertyName("oldValue")]
    public decimal OldValue { get; set; }

    [JsonPropertyName("newValue")]
    public decimal NewValue { get; set; }

    [JsonPropertyName("oldCorrCon")]
    public decimal OldCorrCon { get; set; }

    [JsonPropertyName("newCorrCon")]
    public decimal NewCorrCon { get; set; }
}

public class DfSampleDto
{
    [JsonPropertyName("rowNumber")]
    public int RowNumber { get; set; }

    [JsonPropertyName("solutionLabel")]
    public string SolutionLabel { get; set; } = "";

    [JsonPropertyName("currentDf")]
    public decimal CurrentDf { get; set; }

    [JsonPropertyName("sampleType")]
    public string? SampleType { get; set; }
}

// ============================================
// Correction Service
// ============================================

public class CorrectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CorrectionService> _logger;
    private readonly AuthService _authService;

    public CorrectionService(IHttpClientFactory clientFactory, ILogger<CorrectionService> logger, AuthService authService)
    {
        _httpClient = clientFactory.CreateClient("Api");
        _logger = logger;
        _authService = authService;
    }

    /// <summary>
    /// Find samples with bad weights
    /// </summary>
    public async Task<ServiceResult<List<BadSampleDto>>> FindBadWeightsAsync(Guid projectId, decimal min = 0.190m, decimal max = 0.210m)
    {
        try
        {
            SetAuthHeader();

            var request = new { projectId, weightMin = min, weightMax = max };
            var response = await _httpClient.PostAsJsonAsync("correction/bad-weights", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResult<List<BadSampleDto>>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Succeeded == true && result.Data != null)
                    return ServiceResult<List<BadSampleDto>>.Success(result.Data);

                return ServiceResult<List<BadSampleDto>>.Fail(result?.Message ?? "Failed");
            }

            return ServiceResult<List<BadSampleDto>>.Fail($"Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding bad weights");
            return ServiceResult<List<BadSampleDto>>.Fail($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Find samples with bad volumes
    /// </summary>
    public async Task<ServiceResult<List<BadSampleDto>>> FindBadVolumesAsync(Guid projectId, decimal expectedVolume = 50m)
    {
        try
        {
            SetAuthHeader();

            var request = new { projectId, expectedVolume };
            var response = await _httpClient.PostAsJsonAsync("correction/bad-volumes", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResult<List<BadSampleDto>>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Succeeded == true && result.Data != null)
                    return ServiceResult<List<BadSampleDto>>.Success(result.Data);

                return ServiceResult<List<BadSampleDto>>.Fail(result?.Message ?? "Failed");
            }

            return ServiceResult<List<BadSampleDto>>.Fail($"Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding bad volumes");
            return ServiceResult<List<BadSampleDto>>.Fail($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all DF samples for a project
    /// </summary>
    public async Task<ServiceResult<List<DfSampleDto>>> GetDfSamplesAsync(Guid projectId)
    {
        try
        {
            SetAuthHeader();

            var response = await _httpClient.GetAsync($"correction/{projectId}/df-samples");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResult<List<DfSampleDto>>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Succeeded == true && result.Data != null)
                    return ServiceResult<List<DfSampleDto>>.Success(result.Data);

                return ServiceResult<List<DfSampleDto>>.Fail(result?.Message ?? "Failed");
            }

            return ServiceResult<List<DfSampleDto>>.Fail($"Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting DF samples");
            return ServiceResult<List<DfSampleDto>>.Fail($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Find empty/outlier rows
    /// </summary>
    public async Task<ServiceResult<List<Application.DTOs.EmptyRowDto>>> FindEmptyRowsAsync(Guid projectId, decimal thresholdPercent = 70m)
    {
        try
        {
            SetAuthHeader();

            var request = new { projectId, thresholdPercent };
            var response = await _httpClient.PostAsJsonAsync("correction/empty-rows", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // ✅ Important Change: Explicitly using Application.DTOs.EmptyRowDto
                var result = JsonSerializer.Deserialize<ApiResult<List<Application.DTOs.EmptyRowDto>>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Succeeded == true && result.Data != null)
                {
                    // Now result.Data is of the correct type and will cast without error
                    return ServiceResult<List<Application.DTOs.EmptyRowDto>>.Success(result.Data);
                }

                return ServiceResult<List<Application.DTOs.EmptyRowDto>>.Fail(result?.Message ?? "Failed");
            }

            return ServiceResult<List<Application.DTOs.EmptyRowDto>>.Fail($"Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding empty rows");
            return ServiceResult<List<Application.DTOs.EmptyRowDto>>.Fail($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply weight correction
    /// </summary>
    public async Task<ServiceResult<CorrectionResultDto>> ApplyWeightCorrectionAsync(
        Guid projectId, List<string> solutionLabels, decimal newWeight)
    {
        try
        {
            SetAuthHeader();

            var request = new { projectId, solutionLabels, newWeight };
            var response = await _httpClient.PostAsJsonAsync("correction/weight", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResult<CorrectionResultDto>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Succeeded == true && result.Data != null)
                    return ServiceResult<CorrectionResultDto>.Success(result.Data);

                return ServiceResult<CorrectionResultDto>.Fail(result?.Message ?? "Failed");
            }

            return ServiceResult<CorrectionResultDto>.Fail($"Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying weight correction");
            return ServiceResult<CorrectionResultDto>.Fail($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply volume correction
    /// </summary>
    public async Task<ServiceResult<CorrectionResultDto>> ApplyVolumeCorrectionAsync(
        Guid projectId, List<string> solutionLabels, decimal newVolume)
    {
        try
        {
            SetAuthHeader();

            var request = new { projectId, solutionLabels, newVolume };
            var response = await _httpClient.PostAsJsonAsync("correction/volume", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResult<CorrectionResultDto>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Succeeded == true && result.Data != null)
                    return ServiceResult<CorrectionResultDto>.Success(result.Data);

                return ServiceResult<CorrectionResultDto>.Fail(result?.Message ?? "Failed");
            }

            return ServiceResult<CorrectionResultDto>.Fail($"Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying volume correction");
            return ServiceResult<CorrectionResultDto>.Fail($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply DF correction
    /// </summary>
    public async Task<ServiceResult<CorrectionResultDto>> ApplyDfCorrectionAsync(
        Guid projectId, List<string> solutionLabels, decimal newDf)
    {
        try
        {
            SetAuthHeader();

            var request = new { ProjectId = projectId, SolutionLabels = solutionLabels, NewDf = newDf };
            var response = await _httpClient.PostAsJsonAsync("correction/df", request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("DF Correction response: {StatusCode}, Content: {Content}", 
                response.StatusCode, content);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResult<CorrectionResultDto>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Succeeded == true && result.Data != null)
                    return ServiceResult<CorrectionResultDto>.Success(result.Data);

                return ServiceResult<CorrectionResultDto>.Fail(result?.Message ?? "Failed");
            }

            return ServiceResult<CorrectionResultDto>.Fail($"Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying DF correction");
            return ServiceResult<CorrectionResultDto>.Fail($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete rows by solution labels
    /// </summary>
    public async Task<ServiceResult<int>> DeleteRowsAsync(Guid projectId, List<string> solutionLabels)
    {
        try
        {
            SetAuthHeader();

            var request = new { ProjectId = projectId, SolutionLabels = solutionLabels };
            var response = await _httpClient.PostAsJsonAsync("correction/delete-rows", request);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Delete rows response: {StatusCode}, Content: {Content}", 
                response.StatusCode, content);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResult<int>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Succeeded == true)
                    return ServiceResult<int>.Success(result.Data);

                return ServiceResult<int>.Fail(result?.Message ?? "Failed");
            }

            return ServiceResult<int>.Fail($"Server error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rows");
            return ServiceResult<int>.Fail($"Error: {ex.Message}");
        }
    }

    private void SetAuthHeader()
    {
        var token = _authService.GetAccessToken();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}
