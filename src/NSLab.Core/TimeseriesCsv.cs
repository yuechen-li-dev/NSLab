using System.Globalization;

namespace NSLab.Core;

public sealed record TimeseriesRow(
    double T,
    double E,
    double Z,
    double OmegaInf,
    double DivL2,
    double Dt,
    double Cfl,
    string Status
);

public static class TimeseriesCsv
{
    public const string HeaderExact = "t,E,Z,omega_inf,div_l2,dt,cfl,status";

    public static void WriteHeader(string filePath)
    {
        File.WriteAllText(filePath, HeaderExact + Environment.NewLine);
    }

    public static void AppendRow(string filePath, TimeseriesRow row)
    {
        if (!string.Equals(row.Status, "OK", StringComparison.Ordinal)
            && !string.Equals(row.Status, "WARN", StringComparison.Ordinal)
            && !string.Equals(row.Status, "FAIL", StringComparison.Ordinal))
        {
            throw new ArgumentException("Status must be one of OK WARN FAIL.", nameof(row));
        }

        var line = string.Join(",", new[]
        {
            row.T.ToString("G17", CultureInfo.InvariantCulture),
            row.E.ToString("G17", CultureInfo.InvariantCulture),
            row.Z.ToString("G17", CultureInfo.InvariantCulture),
            row.OmegaInf.ToString("G17", CultureInfo.InvariantCulture),
            row.DivL2.ToString("G17", CultureInfo.InvariantCulture),
            row.Dt.ToString("G17", CultureInfo.InvariantCulture),
            row.Cfl.ToString("G17", CultureInfo.InvariantCulture),
            row.Status
        });

        File.AppendAllText(filePath, line + Environment.NewLine);
    }
}
