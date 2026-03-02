# NSLab

Build:

```bash
dotnet build
```

Test:

```bash
dotnet test
```

Run help:

```bash
dotnet run --project src/NSLab.Cli -- --help
```

## Example workflow

- examples/runs is not committed

1. Validate scenario:

```bash
dotnet run --project src/NSLab.Cli -- validate examples/scenario_sweepable.json
```

2. Run:

```bash
dotnet run --project src/NSLab.Cli -- run examples/scenario_sweepable.json --out examples/runs/demo_run
```

3. Validate run folder:

```bash
dotnet run --project src/NSLab.Cli -- validate examples/runs/demo_run
```

4. Summarize:

```bash
dotnet run --project src/NSLab.Cli -- summarize examples/runs/demo_run
```
