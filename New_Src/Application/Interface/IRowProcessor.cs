namespace Application.Services;

/// <summary>
/// A processor that consumes a parsed row (dictionary from column name to value)
/// and updates an accumulator (dictionary) with computed features.
/// Implementations should be deterministic and side-effect free (stateful accumulator returned).
/// </summary>
public interface IRowProcessor
{
    /// <summary>
    /// Process a single row represented as Dictionary&lt;string, object?&gt; and update accumulator.
    /// </summary>
    void ProcessRow(Dictionary<string, object?> row, Dictionary<string, object?> accumulator);

    /// <summary>
    /// Finalize after all rows processed. Can compute aggregate fields into accumulator.
    /// </summary>
    void Finalize(Dictionary<string, object?> accumulator);
}