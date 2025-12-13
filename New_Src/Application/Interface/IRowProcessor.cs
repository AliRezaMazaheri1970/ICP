namespace Application.Services;

/// <summary>
/// Defines a contract for processing rows of data and accumulating results.
/// Implementations should be deterministic and stateless regarding the instance itself, operating on the provided accumulator.
/// </summary>
public interface IRowProcessor
{
    /// <summary>
    /// Processes a single row of data and updates the provided accumulator.
    /// </summary>
    /// <param name="row">The current row data as a dictionary.</param>
    /// <param name="accumulator">The dictionary to update with processing results.</param>
    void ProcessRow(Dictionary<string, object?> row, Dictionary<string, object?> accumulator);

    /// <summary>
    /// Performs final calculations after all rows have been processed.
    /// </summary>
    /// <param name="accumulator">The accumulated data to finalize.</param>
    void Finalize(Dictionary<string, object?> accumulator);
}