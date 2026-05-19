# Formula Rules Authoring Guide

The calculator reads formula eligibility rules from:

```text
wwwroot/data/formula-rules.json
```

That JSON is the source of truth for formula support, required fields, conditional required fields, and formula-specific numeric ranges. The UI should not contain formula-specific eligibility logic.

## Core Concepts

Each formula is evaluated per eye. The evaluator asks:

1. Does at least one supported combination match the current eye mode?
2. For a matching combination, are the required fields present?
3. Are the required numeric fields inside their valid ranges?

Status precedence is:

```text
Disabled -> UnsupportedCombination -> MissingRequiredFields -> InvalidValues -> Ready
```

Unsupported formulas do not affect the collapsed `Formulas` panel color. Missing fields and invalid values do.

## Top-Level Shape

```json
{
  "fieldRanges": {
    "AL": { "min": 18.0, "max": 35.0, "unit": "mm" }
  },
  "formulas": [
    {
      "id": "hoffer-qst",
      "name": "Hoffer QST",
      "enabled": true,
      "constantField": "HofferPcd",
      "combinations": [],
      "requiredFields": [],
      "conditionalRequiredFields": [],
      "rangeOverrides": {}
    }
  ]
}
```

## Valid Enum Strings

Field ids:

```text
AL, ACD, LT, CCT, WTW,
K1, K2, K1Axis, K2Axis,
PK1, PK2, PK1Axis, PK2Axis,
TK1, TK2, TK1Axis, TK2Axis,
TargetRefraction,
BarrettAConstant, CookeAConstant, EvoAConstant, HillRbfAConstant,
HofferPcd, KaneAConstant, PearlDgsAConstant,
PkDevice
```

Post-LVC/RK modes:

```text
None, MLvc, HLvc, RK
```

Keratometry input sources:

```text
EstimatedPK, PentacamPK, IOLMaster700PK, IOLMaster700TK
```

PK devices:

```text
IOLMaster700, Pentacam
```

`PkDevice` remains a field id because the user may need to choose which measured-device branch is being used. Device support itself is now expressed through `keratometryInputs`.

## Keratometry Input Model

The UI workflow is:

```text
Estimated PK:
  Uses K1/K2 plus internal, non-user-inputtable estimated posterior cornea logic.

Measured PK:
  Pentacam provides PK fields.
  IOLMaster 700 provides PK fields and TK fields.
```

That maps to these JSON values:

```text
EstimatedPK       = estimated PK mode, based on K1/K2 and internal estimator
PentacamPK        = measured PK values from Pentacam
IOLMaster700PK    = measured PK values from IOLMaster 700
IOLMaster700TK    = measured TK values from IOLMaster 700
```

If the eye is in `Estimated` mode, the evaluator offers only:

```json
[ "EstimatedPK" ]
```

If the eye is in `Measured` mode with Pentacam, the evaluator offers:

```json
[ "PentacamPK" ]
```

If the eye is in `Measured` mode with IOLMaster 700, the evaluator offers both:

```json
[ "IOLMaster700PK", "IOLMaster700TK" ]
```

The formula then decides which of those it accepts. This is why no visible `K Source` selector is needed.

## Combinations

`combinations` declares supported mode combinations. A formula is supported when at least one combination matches.

```json
{
  "toric": true,
  "postLvcRk": [ "MLvc" ],
  "keratometryInputs": [ "PentacamPK" ],
  "keratoconus": false,
  "sosAL": false,
  "requiredFields": [ "WTW" ]
}
```

This means:

```text
Toric + M-LVC + measured Pentacam PK + no KC + no SoS AL, additionally mandating WTW for this specific mode.
```

### Combination-Specific Required Fields

While a formula has a base set of required fields defined in `requiredFields` at the formula level, specific combinations can directly mandate other, additional fields using the `"requiredFields"` array inside a combination object. For example:
- Standard eye combination: Only requires standard fields (`AL`, `ACD`, etc.)
- KC combination: Mandates `WTW` in addition to standard fields.

Boolean properties:

```json
"toric": true
```

means exact match. Omitting a boolean means either value is allowed.

Array properties:

```json
"postLvcRk": [ "None", "MLvc" ]
```

means one of those values. Omitting an array or using an empty array means any value is allowed, but explicit arrays are easier to review.

## Common Combination Examples

### Normal Estimated PK

```json
{
  "toric": false,
  "postLvcRk": [ "None" ],
  "keratometryInputs": [ "EstimatedPK" ],
  "keratoconus": false,
  "sosAL": false
}
```

### Formula Supports Estimated PK and Measured PK, But Not TK

```json
"keratometryInputs": [ "EstimatedPK", "PentacamPK", "IOLMaster700PK" ]
```

### Formula Supports IOLMaster 700 TK Only For Measured Input

```json
"keratometryInputs": [ "EstimatedPK", "IOLMaster700TK" ]
```

This allows estimated mode, plus measured IOLMaster 700 TK. It does not allow Pentacam PK or IOLMaster 700 PK.

### Formula Supports Toric + Post-LVC + Pentacam PK

```json
{
  "toric": true,
  "postLvcRk": [ "MLvc", "HLvc" ],
  "keratometryInputs": [ "PentacamPK" ],
  "keratoconus": false,
  "sosAL": false
}
```

### Hoffer QST Supports M-LVC But Not H-LVC/RK

```json
"postLvcRk": [ "None", "MLvc" ]
```

Do not include `HLvc` or `RK`.

## Complex Scenarios and Mutual Exclusivity

Since `combinations` is an array of objects, the evaluator treats the list as a logical **OR** of several **AND** clauses (Disjunctive Normal Form). This gives you the flexibility to define complex, mutually exclusive rules without changing any C# code.

### Example: Toric + Post-LASIK OR Toric + KC, but NOT Post-LASIK + KC

If you want a formula to support:
1. Toric lenses on post-LASIK/post-refractive eyes (`postLvcRk` is `MLvc`, `HLvc`, or `RK`), OR
2. Toric lenses on keratoconus eyes (`keratoconus` is `true`),
3. BUT you want to prevent the combination of both post-refractive surgery and keratoconus concurrently,

You can define this simply and cleanly using two separate combination objects in the `combinations` list:

```json
"combinations": [
  {
    "toric": true,
    "postLvcRk": [ "MLvc", "HLvc", "RK" ],
    "keratoconus": false
  },
  {
    "toric": true,
    "postLvcRk": [ "None" ],
    "keratoconus": true
  }
]
```

#### Why this works:
- If a patient has **Toric + Post-LASIK** (and is `keratoconus: false`), it matches the first combination.
- If a patient has **Toric + Keratoconus** (and is `postLvcRk: None`), it matches the second combination.
- If a patient has **both** (Toric + Keratoconus + Post-LASIK), it will match neither combination:
  - The first combination fails because `keratoconus` is `true` in the eye, but the combination requires `false`.
  - The second combination fails because `postLvcRk` is `MLvc` (or similar) in the eye, but the combination requires `None`.

### Example: Toric + Post-LASIK OR Non-Toric + KC, but NOT Toric + KC

If you want a formula to support:
1. Toric lenses on post-LASIK eyes, OR
2. Non-toric lenses on keratoconus eyes,
3. BUT you do NOT support toric lenses for keratoconus eyes,

You can write:

```json
"combinations": [
  {
    "toric": true,
    "postLvcRk": [ "MLvc", "HLvc", "RK" ]
  },
  {
    "toric": false,
    "postLvcRk": [ "None" ],
    "keratoconus": true
  }
]
```

## Required Fields

`requiredFields` are always required for the formula once a supported combination matches.

```json
"requiredFields": [
  "AL",
  "ACD",
  "K1",
  "K2",
  "TargetRefraction",
  "HofferPcd"
]
```

The formula `constantField` is automatically treated as required, but keeping it in `requiredFields` is acceptable for readability.

## Conditional Required Fields

Use `conditionalRequiredFields` when fields are required only for a specific mode or keratometry input.

### Toric Axes

```json
{
  "when": { "toric": true },
  "fields": [ "K1Axis", "K2Axis" ]
}
```

### Measured PK Values

```json
{
  "when": {
    "keratometryInputs": [ "PentacamPK", "IOLMaster700PK" ]
  },
  "fields": [ "PK1", "PK2", "PkDevice" ]
}
```

### Measured IOLMaster 700 TK Values

```json
{
  "when": {
    "keratometryInputs": [ "IOLMaster700TK" ]
  },
  "fields": [ "TK1", "TK2", "PkDevice" ]
}
```

### Toric Measured PK Axes

```json
{
  "when": {
    "toric": true,
    "keratometryInputs": [ "PentacamPK", "IOLMaster700PK" ]
  },
  "fields": [ "PK1Axis", "PK2Axis" ]
}
```

### Toric IOLMaster 700 TK Axes

```json
{
  "when": {
    "toric": true,
    "keratometryInputs": [ "IOLMaster700TK" ]
  },
  "fields": [ "TK1Axis", "TK2Axis" ]
}
```

## How Alternatives Work

When IOLMaster 700 measured data is selected, the evaluator considers both `IOLMaster700PK` and `IOLMaster700TK` as candidate inputs.

If a formula supports both, it can become ready through either branch:

```json
"keratometryInputs": [ "IOLMaster700PK", "IOLMaster700TK" ]
```

If PK fields are missing but TK fields are present, a formula that supports TK can still be ready. If it supports only PK, it will ask for PK fields.

This is why conditional required fields are evaluated against each candidate keratometry input, not globally against the whole eye.

## Field Ranges

Default ranges live under `fieldRanges`:

```json
"fieldRanges": {
  "AL": { "min": 18.0, "max": 35.0, "unit": "mm" },
  "TargetRefraction": { "min": -10.0, "max": 5.0, "unit": "D" }
}
```

Ranges are checked only for fields required by the matched formula/input branch.

Use `rangeOverrides` for formula-specific limits:

```json
"rangeOverrides": {
  "AL": { "min": 16.0, "max": 26.0, "unit": "mm" }
}
```

Overrides merge with defaults. If an override only sets `max`, the default `min` and `unit` are retained.

## Adding Or Editing A Formula

1. Confirm any constant exists in `FieldId`.
2. Add or confirm default ranges in `fieldRanges`.
3. Add a formula object with stable `id`, display `name`, `enabled`, and `constantField`.
4. Add explicit `combinations`.
5. Use `EstimatedPK`, `PentacamPK`, `IOLMaster700PK`, and `IOLMaster700TK` to describe keratometry support.
6. Add base `requiredFields`.
7. Add conditional fields for toric axes, measured PK fields, measured TK fields, and measured axes.
8. Add `rangeOverrides` only where the formula differs from defaults.
9. Validate:

```bash
jq . wwwroot/data/formula-rules.json
/usr/local/share/dotnet/dotnet build
```

## Common Mistakes

### Reintroducing Generic PK/TK

Do not write:

```json
"keratometry": [ "PK", "TK" ]
```

Write:

```json
"keratometryInputs": [ "EstimatedPK", "PentacamPK", "IOLMaster700PK", "IOLMaster700TK" ]
```

### Confusing Device With Input Source

Do not write a separate device allow-list. Device is encoded in the input source:

```json
"keratometryInputs": [ "PentacamPK" ]
```

means Pentacam measured PK only.

```json
"keratometryInputs": [ "IOLMaster700TK" ]
```

means IOLMaster 700 measured TK only.

### Making Axes Always Required

Do not put axis fields in `requiredFields` unless every supported mode needs them.

Use conditional fields:

```json
{ "when": { "toric": true }, "fields": [ "K1Axis", "K2Axis" ] }
```

### Accidentally Supporting Every Post-LVC/RK Mode

This means any post-LVC/RK value:

```json
"postLvcRk": []
```

For normal eyes only:

```json
"postLvcRk": [ "None" ]
```

For normal and M-LVC:

```json
"postLvcRk": [ "None", "MLvc" ]
```

## When JSON Is Enough

Edit JSON for:

```text
Formula support
Formula enablement defaults
Required fields
Conditional fields
Ranges and formula-specific range overrides
```

Change C# only for:

```text
New field ids
New post-LVC/RK modes
New keratometry input source types
New PK devices
Different validation precedence
New validation types beyond presence/ranges
```
