namespace Application.DTOs;

/// <summary>
/// Represents a request to create or retrieve a pivot table.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="SearchText">Optional text to filter rows.</param>
/// <param name="SelectedSolutionLabels">Optional list of specific solution labels.</param>
/// <param name="SelectedElements">Optional list of specific elements.</param>
/// <param name="NumberFilters">Optional numeric filters per column.</param>
/// <param name="UseOxide">Whether to convert to oxide values.</param>
/// <param name="DecimalPlaces">Decimal precision for display.</param>
/// <param name="Page">Page number.</param>
/// <param name="PageSize">Page size.</param>
public record PivotRequest(
    Guid ProjectId,
    string? SearchText = null,
    List<string>? SelectedSolutionLabels = null,
    List<string>? SelectedElements = null,
    Dictionary<string, NumberFilter>? NumberFilters = null,
    bool UseOxide = false,
    int DecimalPlaces = 2,
    int Page = 1,
    int PageSize = 100
);

/// <summary>
/// Represents a numeric range filter for a column.
/// </summary>
/// <param name="Min">Minimum value (inclusive).</param>
/// <param name="Max">Maximum value (inclusive).</param>
public record NumberFilter(
    decimal? Min = null,
    decimal? Max = null
);

/// <summary>
/// Represents the result of a pivot table operation.
/// </summary>
/// <param name="Columns">List of column headers.</param>
/// <param name="Rows">List of data rows.</param>
/// <param name="TotalCount">Total number of rows.</param>
/// <param name="Page">Current page number.</param>
/// <param name="PageSize">Current page size.</param>
/// <param name="Metadata">Metadata and statistics.</param>
public record PivotResultDto(
    List<string> Columns,
    List<PivotRowDto> Rows,
    int TotalCount,
    int Page,
    int PageSize,
    PivotMetadataDto Metadata
);

/// <summary>
/// Represents a single row in a pivot table.
/// </summary>
/// <param name="SolutionLabel">The sample label.</param>
/// <param name="Values">The dictionary of values per column.</param>
/// <param name="OriginalIndex">The original row index.</param>
public record PivotRowDto(
    string SolutionLabel,
    Dictionary<string, decimal?> Values,
    int OriginalIndex
);

/// <summary>
/// Contains metadata and statistics for the pivot table.
/// </summary>
/// <param name="AllSolutionLabels">List of all available solution labels.</param>
/// <param name="AllElements">List of all available elements.</param>
/// <param name="ColumnStats">Statistics for each column.</param>
public record PivotMetadataDto(
    List<string> AllSolutionLabels,
    List<string> AllElements,
    Dictionary<string, ColumnStatsDto> ColumnStats
);

/// <summary>
/// Represents statistics for a data column.
/// </summary>
/// <param name="Min">Minimum value.</param>
/// <param name="Max">Maximum value.</param>
/// <param name="Mean">Mean value.</param>
/// <param name="StdDev">Standard deviation.</param>
/// <param name="NonNullCount">Count of non-null values.</param>
public record ColumnStatsDto(
    decimal? Min,
    decimal? Max,
    decimal? Mean,
    decimal? StdDev,
    int NonNullCount
);

/// <summary>
/// Represents a request to detect duplicate samples.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="ThresholdPercent">The percentage threshold for difference.</param>
/// <param name="DuplicatePatterns">Optional patterns to identify duplicates.</param>
public record DuplicateDetectionRequest(
    Guid ProjectId,
    decimal ThresholdPercent = 10m,
    List<string>? DuplicatePatterns = null
);

/// <summary>
/// Represents the result of a duplicate check.
/// </summary>
/// <param name="MainSolutionLabel">The primary sample label.</param>
/// <param name="DuplicateSolutionLabel">The duplicate sample label.</param>
/// <param name="Differences">List of differences per element.</param>
/// <param name="HasOutOfRangeDiff">Indicates if significant differences exist.</param>
public record DuplicateResultDto(
    string MainSolutionLabel,
    string DuplicateSolutionLabel,
    List<ElementDiffDto> Differences,
    bool HasOutOfRangeDiff
);

/// <summary>
/// Provides standard oxide conversion factors.
/// </summary>
public static class OxideFactors
{
    /// <summary>
    /// Dictionary of element symbols to their oxide formula and conversion factor.
    /// </summary>
    public static readonly Dictionary<string, (string Formula, decimal Factor)> Factors = new()
    {
        { "Ag", ("Ag2O", 1.0741m) },
        { "Al", ("Al2O3", 1.8895m) },
        { "As", ("As2O5", 1.5339m) },
        { "Au", ("Au2O3", 1.1218m) },
        { "B",  ("B2O3", 3.2199m) },
        { "Ba", ("BaO", 1.1165m) },
        { "Be", ("BeO", 2.7753m) },
        { "Bi", ("Bi2O3", 1.1148m) },
        { "Br", ("Br", 1.0m) },
        { "C",  ("CO2", 3.6641m) },
        { "Ca", ("CaO", 1.3992m) },
        { "Cd", ("CdO", 1.1423m) },
        { "Ce", ("CeO2", 1.2284m) },
        { "Cl", ("Cl", 1.0m) },
        { "Co", ("CoO", 1.2715m) },
        { "Cr", ("Cr2O3", 1.4616m) },
        { "Cs", ("Cs2O", 1.0602m) },
        { "Cu", ("CuO", 1.2518m) },
        { "Dy", ("Dy2O3", 1.1477m) },
        { "Er", ("Er2O3", 1.1435m) },
        { "Eu", ("Eu2O3", 1.1579m) },
        { "F",  ("F", 1.0m) },
        { "Fe", ("Fe2O3", 1.4297m) },
        { "Ga", ("Ga2O3", 1.3442m) },
        { "Gd", ("Gd2O3", 1.1526m) },
        { "Ge", ("GeO2", 1.4408m) },
        { "Hf", ("HfO2", 1.1793m) },
        { "Hg", ("HgO", 1.0798m) },
        { "Ho", ("Ho2O3", 1.1455m) },
        { "I",  ("I", 1.0m) },
        { "In", ("In2O3", 1.2091m) },
        { "Ir", ("IrO2", 1.1665m) },
        { "K",  ("K2O", 1.2046m) },
        { "La", ("La2O3", 1.1728m) },
        { "Li", ("Li2O", 2.1527m) },
        { "Lu", ("Lu2O3", 1.1371m) },
        { "Mg", ("MgO", 1.6583m) },
        { "Mn", ("MnO", 1.2912m) },
        { "Mo", ("MoO3", 1.5003m) },
        { "N",  ("N", 1.0m) },
        { "Na", ("Na2O", 1.3480m) },
        { "Nb", ("Nb2O5", 1.4305m) },
        { "Nd", ("Nd2O3", 1.1664m) },
        { "Ni", ("NiO", 1.2725m) },
        { "Os", ("OsO4", 1.3365m) },
        { "P",  ("P2O5", 2.2914m) },
        { "Pb", ("PbO", 1.0772m) },
        { "Pd", ("PdO", 1.1503m) },
        { "Pr", ("Pr6O11", 1.2082m) },
        { "Pt", ("PtO2", 1.1639m) },
        { "Rb", ("Rb2O", 1.0936m) },
        { "Re", ("Re2O7", 1.3009m) },
        { "Rh", ("Rh2O3", 1.2332m) },
        { "Ru", ("RuO2", 1.3165m) },
        { "S",  ("SO3", 2.4972m) },
        { "Sb", ("Sb2O3", 1.1971m) },
        { "Sc", ("Sc2O3", 1.5338m) },
        { "Se", ("SeO2", 1.4053m) },
        { "Si", ("SiO2", 2.1393m) },
        { "Sm", ("Sm2O3", 1.1596m) },
        { "Sn", ("SnO2", 1.2696m) },
        { "Sr", ("SrO", 1.1826m) },
        { "Ta", ("Ta2O5", 1.2211m) },
        { "Tb", ("Tb4O7", 1.1762m) },
        { "Tc", ("Tc2O7", 1.5657m) },
        { "Te", ("TeO2", 1.2508m) },
        { "Th", ("ThO2", 1.1379m) },
        { "Ti", ("TiO2", 1.6681m) },
        { "Tl", ("Tl2O3", 1.1158m) },
        { "Tm", ("Tm2O3", 1.1421m) },
        { "U",  ("U3O8", 1.1792m) },
        { "V",  ("V2O5", 1.7852m) },
        { "W",  ("WO3", 1.2611m) },
        { "Y",  ("Y2O3", 1.2699m) },
        { "Yb", ("Yb2O3", 1.1387m) },
        { "Zn", ("ZnO", 1.2447m) },
        { "Zr", ("ZrO2", 1.3508m) }
    };
}