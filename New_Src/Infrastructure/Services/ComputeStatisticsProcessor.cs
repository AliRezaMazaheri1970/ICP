using Application.Services;
using System.Text.Json;

namespace Infrastructure.Services.Processors;

/// <summary>
/// For numeric columns in rows, compute count, mean and stddev.
/// Assumes row values are numbers or strings parseable to double.
/// Results written into accumulator with keys like: stats_{column}_count, stats_{column}_mean, stats_{column}_std
/// </summary>
public class ComputeStatisticsProcessor : IRowProcessor
{
    // hold intermediate stats per column using lists or Welford's algorithm
    private class StatAccum
    {
        public long Count;
        public double Mean;
        public double M2; // for variance algorithm
    }

    public void ProcessRow(Dictionary<string, object?> row, Dictionary<string, object?> accumulator)
    {
        // Use a nested dictionary in accumulator to keep per-column stats (create if missing)
        if (!accumulator.TryGetValue("__stats_internal", out var obj) || obj is not Dictionary<string, StatAccum> stats)
        {
            stats = new Dictionary<string, StatAccum>(StringComparer.OrdinalIgnoreCase);
            accumulator["__stats_internal"] = stats;
        }

        foreach (var kv in row)
        {
            var col = kv.Key;
            var val = kv.Value;
            if (val == null) continue;

            if (val is JsonElement je)
            {
                // try to convert JsonElement to numeric or string
                if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetDouble(out var dnum))
                {
                    AddValue(stats, col, dnum);
                }
                else if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = je.GetString();
                    if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d2))
                        AddValue(stats, col, d2);
                }
            }
            else if (val is double d)
            {
                AddValue(stats, col, d);
            }
            else if (val is float f)
            {
                AddValue(stats, col, f);
            }
            else if (val is long l)
            {
                AddValue(stats, col, l);
            }
            else if (val is int i)
            {
                AddValue(stats, col, i);
            }
            else if (val is string ss)
            {
                if (double.TryParse(ss, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d3))
                    AddValue(stats, col, d3);
            }
            // else ignore non-numeric columns
        }
    }

    public void Finalize(Dictionary<string, object?> accumulator)
    {
        if (!accumulator.TryGetValue("__stats_internal", out var obj) || obj is not Dictionary<string, StatAccum> stats) return;

        foreach (var kv in stats)
        {
            var col = kv.Key;
            var acc = kv.Value;
            var count = acc.Count;
            var mean = acc.Mean;
            var variance = count > 1 ? (acc.M2 / (count - 1)) : 0;
            var std = Math.Sqrt(Math.Max(0, variance));

            accumulator[$"stats_{col}_count"] = count;
            accumulator[$"stats_{col}_mean"] = mean;
            accumulator[$"stats_{col}_std"] = std;
        }

        // remove internal
        accumulator.Remove("__stats_internal");
    }

    private void AddValue(Dictionary<string, StatAccum> stats, string col, double value)
    {
        if (!stats.TryGetValue(col, out var st))
        {
            st = new StatAccum { Count = 0, Mean = 0, M2 = 0 };
            stats[col] = st;
        }

        st.Count++;
        var delta = value - st.Mean;
        st.Mean += delta / st.Count;
        var delta2 = value - st.Mean;
        st.M2 += delta * delta2;
    }
}