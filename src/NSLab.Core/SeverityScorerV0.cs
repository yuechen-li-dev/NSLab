namespace NSLab.Core;

public static class SeverityScorerV0
{
    public static (double Severity, string HealthStatus) Compute(IReadOnlyList<TimeseriesRow> rows)
    {
        if (rows.Count == 0)
        {
            return (0d, HealthStatus.OK);
        }

        foreach (var row in rows)
        {
            if (string.Equals(row.Status, "FAIL", StringComparison.Ordinal))
            {
                return (0d, HealthStatus.FAIL_NAN);
            }
        }

        var healthStatus = HealthStatus.OK;
        foreach (var row in rows)
        {
            if (string.Equals(row.Status, "WARN", StringComparison.Ordinal))
            {
                healthStatus = HealthStatus.WARN_NUMERIC;
                break;
            }
        }

        var maxOmega = double.MinValue;
        var maxZ = double.MinValue;

        foreach (var row in rows)
        {
            if (row.OmegaInf > maxOmega)
            {
                maxOmega = row.OmegaInf;
            }

            if (row.Z > maxZ)
            {
                maxZ = row.Z;
            }
        }

        var omegaGrowth = rows[^1].OmegaInf - rows[0].OmegaInf;
        var severity = maxOmega + (0.1 * omegaGrowth) + (0.01 * maxZ);
        if (severity < 0d)
        {
            severity = 0d;
        }

        return (severity, healthStatus);
    }
}
