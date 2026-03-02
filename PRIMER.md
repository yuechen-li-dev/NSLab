# NSLab PRIMER

This primer is for a fresh LLM operator running NSLab deterministically.
Use this document as a strict operational contract.
Do not invent fields, commands, or schema versions.
Prefer repeatable runs over creative runs.

## Contract overview

Your job is to transform a scenario into an evidence pack and then into a next step decision.
The core contract is command stable and schema stable.
Use only these command surfaces:
- nslab validate
- nslab run
- nslab summarize

Always treat validation as a gate.
If validate fails do not proceed.

Inputs and outputs are file based.
You write scenario.json, execute one run, and inspect summary.json.
Every run must be traceable and reproducible.

## Inputs scenario.json

scenario.json is the single run intent.
It defines setup, numerics, output cadence, and traceability fields.
Include experiment metadata for auditability.
Required traceability keys include:
- llm_name
- run_number
- utc_timestamp

Traceability rules:
- Run numbering must be monotonically increasing per LLM.
- utc_timestamp must be ISO 8601 Z.
- These fields appear in scenario.json and are mirrored in metadata.json and summary.json.

Use a deterministic seed and keep the template structure stable across iterations.
Only change what your decision policy requires.

```json
{
  "schema_version": "nslab.scenario.v0",
  "experiment": {
    "llm_name": "REPLACE_ME",
    "run_number": 1,
    "utc_timestamp": "REPLACE_ME"
  },
  "solver": null,
  "domain": {
    "length": 1.0,
    "resolution": 128
  },
  "resolution": {
    "N": 128
  },
  "time": {
    "t_end": 1.0,
    "timeseries_dt": 0.01
  },
  "physics": {
    "nu": 0.01
  },
  "initial_condition": {
    "kind": "gaussian"
  },
  "numerics": {
    "cfl": 0.5
  },
  "outputs": {
    "timeseries": true,
    "spectra": false,
    "snapshots": false
  },
  "seed": 12345
}
```

## Outputs evidence pack

A run folder is the evidence pack.
It must be validated before use.
Primary files and purpose:
- metadata.json is provenance and resolved scenario.
- timeseries.csv is the raw diagnostic time history.
- summary.json is the LLM friendly decision surface.

Treat summary.json as decision input, not as a substitute for failed validation.
When in doubt, verify using a tighter rerun.

## Severity and health flags

Read severity and health together.
Key summary fields include:
- severity_score_v0
- max_omega_inf
- health status indicator

HealthStatus values:
- OK
- WARN_NUMERIC
- FAIL_DIVERGENCE
- FAIL_CFL
- FAIL_NAN

Interpretation rules:
- OK means base numerical behavior is acceptable for current settings.
- WARN_NUMERIC means usable with caution and likely needs verification.
- FAIL means the run is not trustworthy and should trigger verify steps or parameter rollback.

## Explore vs verify policy

Use this decision split:
- Explore when severity is low and status is OK.
- Verify when severity is high or status is not OK.

Verify means rerun with tighter numerics or higher resolution and compare outcomes.
A verify run changes one stability or resolution control at a time.
Do not trust apparent blow up without refinement checks.

## Refinement rules

When verifying, apply controlled deltas only.
Allowed refinement actions include:
- increase resolution N
- decrease timeseries_dt or tighten cfl
- keep everything else constant for verification runs
- update run_number and utc_timestamp each run

Never mix many changes in one verify step.
Use minimal deltas so cause and effect stays clear.

## Anti false positive rules

Guard against noise and transient artifacts.
Operational safeguards:
- insist on validate passing for scenario and run folder
- check determinism by rerunning same scenario if suspicious
- do not chase single run anomalies

A single alarming run is a signal to verify, not to conclude.
Promote conclusions only when repeated evidence agrees.

## Standard runbook

Execute this exact loop for each run:
1 write scenario
2 validate scenario
3 run
4 validate run folder
5 summarize
6 decide next scenario delta

Use this command sequence exactly once per run folder.

```sh
nslab validate scenario.json
nslab run --scenario scenario.json --out runs/run_0001
nslab validate runs/run_0001
nslab summarize runs/run_0001
```

## Failure handling

Failure handling is deterministic.
If scenario validation fails, fix scenario structure and rerun validation.
If run folder validation fails, treat outputs as non authoritative.
If health is FAIL, do not continue exploration from that output.

Recovery policy:
- rollback to last trustworthy configuration
- apply one refinement action
- rerun and compare summary metrics
- continue only after validation and coherent trend

Keep decision notes short and factual.
Avoid speculative causal claims without verify evidence.

## Minimal acceptance drill

Use this checklist to confirm a fresh LLM can operate the workflow:
- ask it to generate scenario.json from this primer
- run commands
- paste summary.json back
- ask it for next scenario.json with run_number incremented

Acceptance expectation:
- it emits valid scenario structure
- it follows validate gate behavior
- it increments traceability correctly
- it chooses explore or verify from status plus severity

END
