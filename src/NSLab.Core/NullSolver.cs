namespace NSLab.Core;

public sealed record NullSolverResult(
    IReadOnlyList<TimeseriesRow> Rows,
    double MaxOmegaInf,
    double MaxZ,
    double MaxE,
    double TAtMaxOmegaInf
);

public static class NullSolver
{
    public static NullSolverResult Execute(ScenarioV0 scenario)
    {
        const int steps = 100;
        var dt = scenario.Time.TEnd / steps;
        var baseOmega = (scenario.Seed % 10) * 0.01 + 0.1;

        var rows = new List<TimeseriesRow>(steps + 1);
        for (var i = 0; i < steps; i++)
        {
            var t = i * dt;
            var omega = baseOmega + Math.Sin(t * 2.0) * 0.1;
            var e = 1.0 + 0.01 * i;
            var z = 0.5 + 0.02 * i;
            rows.Add(new TimeseriesRow(t, e, z, omega, 0.0, dt, scenario.Time.Cfl, "OK"));
        }

        var maxOmega = double.MinValue;
        var maxOmegaTime = 0.0;
        var maxZ = double.MinValue;
        var maxE = double.MinValue;

        foreach (var row in rows)
        {
            if (row.OmegaInf > maxOmega)
            {
                maxOmega = row.OmegaInf;
                maxOmegaTime = row.T;
            }

            if (row.Z > maxZ)
            {
                maxZ = row.Z;
            }

            if (row.E > maxE)
            {
                maxE = row.E;
            }
        }

        return new NullSolverResult(rows, maxOmega, maxZ, maxE, maxOmegaTime);
    }
}
