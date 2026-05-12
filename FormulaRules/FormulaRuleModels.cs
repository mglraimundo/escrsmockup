using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace escrsmockup.FormulaRules;

public static class FormulaRuleJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

public enum FieldId
{
    AL,
    ACD,
    LT,
    CCT,
    WTW,
    K1,
    K2,
    K1Axis,
    K2Axis,
    PK1,
    PK2,
    PK1Axis,
    PK2Axis,
    TK1,
    TK2,
    TK1Axis,
    TK2Axis,
    TargetRefraction,
    BarrettAConstant,
    CookeAConstant,
    EvoAConstant,
    HillRbfAConstant,
    HofferPcd,
    KaneAConstant,
    PearlDgsAConstant,
    PkDevice
}

public enum PostLvcRkType
{
    None,
    MLvc,
    HLvc,
    RK
}

public enum KeratometryInput
{
    EstimatedPK,
    PentacamPK,
    IOLMaster700PK,
    IOLMaster700TK
}

public enum PkDevice
{
    IOLMaster700,
    Pentacam
}

public enum FormulaEligibilityStatus
{
    Ready,
    MissingRequiredFields,
    InvalidValues,
    UnsupportedCombination,
    Disabled
}

public sealed class FormulaRuleSet
{
    public Dictionary<FieldId, FieldRangeDefinition> FieldRanges { get; set; } = new();

    public List<FormulaRule> Formulas { get; set; } = new();
}

public sealed class FormulaRule
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public FieldId? ConstantField { get; set; }

    public List<FormulaCombination> Combinations { get; set; } = new();

    public List<FieldId> RequiredFields { get; set; } = new();

    public List<ConditionalRequiredFields> ConditionalRequiredFields { get; set; } = new();

    public Dictionary<FieldId, FieldRangeDefinition> RangeOverrides { get; set; } = new();
}

public sealed class FormulaCombination
{
    public bool? Toric { get; set; }

    public List<PostLvcRkType> PostLvcRk { get; set; } = new();

    public List<KeratometryInput> KeratometryInputs { get; set; } = new();

    public bool? Keratoconus { get; set; }

    public bool? SosAL { get; set; }
}

public sealed class ConditionalRequiredFields
{
    public FormulaCondition When { get; set; } = new();

    public List<FieldId> Fields { get; set; } = new();
}

public sealed class FormulaCondition
{
    public bool? Toric { get; set; }

    public List<PostLvcRkType> PostLvcRk { get; set; } = new();

    public List<KeratometryInput> KeratometryInputs { get; set; } = new();

    public bool? Keratoconus { get; set; }

    public bool? SosAL { get; set; }
}

public sealed class FieldRangeDefinition
{
    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public string? Unit { get; set; }
}

public sealed record FormulaEyeSnapshot(
    bool IsEnabled,
    bool IsToric,
    PostLvcRkType PostLvcRk,
    bool IsKeratoconus,
    bool IsSosAL,
    IReadOnlyList<KeratometryInput> AvailableKeratometryInputs,
    PkDevice? PkDevice,
    IReadOnlyDictionary<FieldId, string> Values);

public sealed record FormulaEligibilityResult(
    FormulaRule Formula,
    FormulaEligibilityStatus Status,
    IReadOnlyList<FieldId> MissingFields,
    IReadOnlyList<string> InvalidMessages,
    IReadOnlyList<string> UnsupportedMessages)
{
    public string Summary => Status switch
    {
        FormulaEligibilityStatus.Ready => "Ready",
        FormulaEligibilityStatus.Disabled => "Disabled",
        FormulaEligibilityStatus.MissingRequiredFields => $"Missing: {FormatMissingFields(MissingFields)}",
        FormulaEligibilityStatus.InvalidValues => InvalidMessages.FirstOrDefault() ?? "Invalid values",
        FormulaEligibilityStatus.UnsupportedCombination => UnsupportedMessages.FirstOrDefault() ?? "Unsupported combination",
        _ => Status.ToString()
    };

    private static string FormatMissingFields(IReadOnlyList<FieldId> fields)
    {
        var labels = fields.Select(FormulaFieldLabels.GetLabel).Take(3).ToList();
        return fields.Count > 3
            ? $"{string.Join(", ", labels)}..."
            : string.Join(", ", labels);
    }
}

public static class FormulaFieldLabels
{
    private static readonly Dictionary<FieldId, string> Labels = new()
    {
        [FieldId.AL] = "AL",
        [FieldId.ACD] = "ACD",
        [FieldId.LT] = "LT",
        [FieldId.CCT] = "CCT",
        [FieldId.WTW] = "WTW",
        [FieldId.K1] = "K1",
        [FieldId.K2] = "K2",
        [FieldId.K1Axis] = "K1 axis",
        [FieldId.K2Axis] = "K2 axis",
        [FieldId.PK1] = "PK1",
        [FieldId.PK2] = "PK2",
        [FieldId.PK1Axis] = "PK1 axis",
        [FieldId.PK2Axis] = "PK2 axis",
        [FieldId.TK1] = "TK1",
        [FieldId.TK2] = "TK2",
        [FieldId.TK1Axis] = "TK1 axis",
        [FieldId.TK2Axis] = "TK2 axis",
        [FieldId.TargetRefraction] = "Target",
        [FieldId.BarrettAConstant] = "Barrett A",
        [FieldId.CookeAConstant] = "Cooke A",
        [FieldId.EvoAConstant] = "EVO A",
        [FieldId.HillRbfAConstant] = "Hill-RBF A",
        [FieldId.HofferPcd] = "Hoffer pACD",
        [FieldId.KaneAConstant] = "Kane A",
        [FieldId.PearlDgsAConstant] = "Pearl DGS A",
        [FieldId.PkDevice] = "PK device"
    };

    public static string GetLabel(FieldId field) => Labels.GetValueOrDefault(field, field.ToString());
}

public static class FormulaValueParser
{
    public static bool TryParseDecimal(string value, out decimal parsed)
    {
        parsed = 0;
        var normalizedValue = value.Replace(',', '.');
        return decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
    }
}
