namespace escrsmockup.FormulaRules;

public static class FormulaEligibilityEvaluator
{
    private sealed record FormulaCombinationContext(
        FormulaCombination Combination,
        KeratometryInput? KeratometryInput);

    public static List<FormulaEligibilityResult> EvaluateAll(FormulaRuleSet ruleSet, FormulaEyeSnapshot eye)
    {
        return ruleSet.Formulas
            .Select(formula => Evaluate(ruleSet, formula, eye))
            .ToList();
    }

    public static FormulaEligibilityResult Evaluate(FormulaRuleSet ruleSet, FormulaRule formula, FormulaEyeSnapshot eye)
    {
        if (!formula.Enabled || !eye.IsEnabled)
        {
            return new FormulaEligibilityResult(
                formula,
                FormulaEligibilityStatus.Disabled,
                Array.Empty<FieldId>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var matchingContexts = GetMatchingCombinationContexts(formula, eye);
        if (matchingContexts.Count == 0)
        {
            return new FormulaEligibilityResult(
                formula,
                FormulaEligibilityStatus.UnsupportedCombination,
                Array.Empty<FieldId>(),
                Array.Empty<string>(),
                GetUnsupportedMessages(formula, eye));
        }

        var candidateResults = matchingContexts
            .Select(context => EvaluateCandidate(ruleSet, formula, eye, context))
            .ToList();

        var readyResult = candidateResults.FirstOrDefault(result => result.Status == FormulaEligibilityStatus.Ready);
        if (readyResult is not null)
        {
            return readyResult;
        }

        var missingResult = candidateResults
            .Where(result => result.Status == FormulaEligibilityStatus.MissingRequiredFields)
            .OrderBy(result => result.MissingFields.Count)
            .FirstOrDefault();
        if (missingResult is not null)
        {
            return missingResult;
        }

        var invalidResult = candidateResults.FirstOrDefault(result => result.Status == FormulaEligibilityStatus.InvalidValues);
        if (invalidResult is not null)
        {
            return invalidResult;
        }

        return candidateResults.First();
    }

    public static IReadOnlyList<FieldId> GetRequiredFields(FormulaRule formula, FormulaEyeSnapshot eye)
    {
        return GetMatchingCombinationContexts(formula, eye)
            .SelectMany(context => GetRequiredFields(formula, eye, context))
            .Distinct()
            .ToList();
    }

    private static FormulaEligibilityResult EvaluateCandidate(
        FormulaRuleSet ruleSet,
        FormulaRule formula,
        FormulaEyeSnapshot eye,
        FormulaCombinationContext context)
    {
        var requiredFields = GetRequiredFields(formula, eye, context);
        var missingFields = requiredFields
            .Where(field => !HasValue(field, eye))
            .Distinct()
            .ToList();

        if (missingFields.Count > 0)
        {
            return new FormulaEligibilityResult(
                formula,
                FormulaEligibilityStatus.MissingRequiredFields,
                missingFields,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var invalidMessages = GetInvalidMessages(ruleSet, formula, eye, requiredFields);
        if (invalidMessages.Count > 0)
        {
            return new FormulaEligibilityResult(
                formula,
                FormulaEligibilityStatus.InvalidValues,
                Array.Empty<FieldId>(),
                invalidMessages,
                Array.Empty<string>());
        }

        return new FormulaEligibilityResult(
            formula,
            FormulaEligibilityStatus.Ready,
            Array.Empty<FieldId>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static IReadOnlyList<FieldId> GetRequiredFields(
        FormulaRule formula,
        FormulaEyeSnapshot eye,
        FormulaCombinationContext context)
    {
        var fields = formula.RequiredFields.ToList();

        foreach (var conditional in formula.ConditionalRequiredFields.Where(conditional => Matches(conditional.When, eye, context)))
        {
            fields.AddRange(conditional.Fields);
        }

        if (formula.ConstantField is { } constantField && !fields.Contains(constantField))
        {
            fields.Add(constantField);
        }

        return fields.Distinct().ToList();
    }

    public static FieldRangeDefinition? GetEffectiveRange(FormulaRuleSet ruleSet, FormulaRule formula, FieldId field)
    {
        var hasDefault = ruleSet.FieldRanges.TryGetValue(field, out var defaultRange);
        var hasOverride = formula.RangeOverrides.TryGetValue(field, out var overrideRange);

        if (!hasDefault && !hasOverride)
        {
            return null;
        }

        return new FieldRangeDefinition
        {
            Min = overrideRange?.Min ?? defaultRange?.Min,
            Max = overrideRange?.Max ?? defaultRange?.Max,
            Unit = overrideRange?.Unit ?? defaultRange?.Unit
        };
    }

    private static List<string> GetUnsupportedMessages(FormulaRule formula, FormulaEyeSnapshot eye)
    {
        var messages = new List<string>();
        var allCombinations = formula.Combinations;

        if (!allCombinations.Any(combination => !combination.Toric.HasValue || combination.Toric.Value == eye.IsToric))
        {
            messages.Add(eye.IsToric
                ? $"{formula.Name} does not support toric mode."
                : $"{formula.Name} does not support non-toric mode.");
        }

        if (!allCombinations.Any(combination => Supports(combination.PostLvcRk, eye.PostLvcRk)))
        {
            messages.Add($"{formula.Name} supports {FormatSupportedPostLvc(allCombinations)}.");
        }

        if (!allCombinations.Any(combination => SupportsAny(combination.KeratometryInputs, eye.AvailableKeratometryInputs)))
        {
            messages.Add($"{formula.Name} supports {FormatSupportedKeratometryInputs(allCombinations)}.");
        }

        if (!allCombinations.Any(combination => !combination.Keratoconus.HasValue || combination.Keratoconus.Value == eye.IsKeratoconus))
        {
            messages.Add(eye.IsKeratoconus
                ? $"{formula.Name} does not support KC."
                : $"{formula.Name} requires KC.");
        }

        if (!allCombinations.Any(combination => !combination.SosAL.HasValue || combination.SosAL.Value == eye.IsSosAL))
        {
            messages.Add(eye.IsSosAL
                ? $"{formula.Name} does not support SoS AL."
                : $"{formula.Name} requires SoS AL.");
        }

        if (messages.Count == 0)
        {
            messages.Add($"{formula.Name} does not support this combination.");
        }

        return messages.Distinct().ToList();
    }

    private static List<string> GetInvalidMessages(
        FormulaRuleSet ruleSet,
        FormulaRule formula,
        FormulaEyeSnapshot eye,
        IReadOnlyList<FieldId> fields)
    {
        var invalidMessages = new List<string>();

        foreach (var field in fields)
        {
            if (!eye.Values.TryGetValue(field, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            var range = GetEffectiveRange(ruleSet, formula, field);
            if (range is null)
            {
                continue;
            }

            if (!FormulaValueParser.TryParseDecimal(rawValue, out var value))
            {
                invalidMessages.Add($"{FormulaFieldLabels.GetLabel(field)} must be numeric.");
                continue;
            }

            if (range.Min is { } min && value < min || range.Max is { } max && value > max)
            {
                invalidMessages.Add($"{FormulaFieldLabels.GetLabel(field)} must be {FormatRange(range)}.");
            }
        }

        return invalidMessages;
    }

    private static List<FormulaCombinationContext> GetMatchingCombinationContexts(FormulaRule formula, FormulaEyeSnapshot eye)
    {
        if (formula.Combinations.Count == 0)
        {
            return eye.AvailableKeratometryInputs.Count == 0
                ? new List<FormulaCombinationContext> { new(new FormulaCombination(), null) }
                : eye.AvailableKeratometryInputs
                    .Select(input => new FormulaCombinationContext(new FormulaCombination(), input))
                    .ToList();
        }

        return formula.Combinations
            .Where(combination => MatchesStaticDimensions(combination, eye))
            .SelectMany(combination => GetMatchingKeratometryInputs(combination, eye)
                .Select(input => new FormulaCombinationContext(combination, input)))
            .ToList();
    }

    private static IReadOnlyList<KeratometryInput?> GetMatchingKeratometryInputs(FormulaCombination combination, FormulaEyeSnapshot eye)
    {
        var availableInputs = eye.AvailableKeratometryInputs;

        if (combination.KeratometryInputs.Count == 0)
        {
            return availableInputs.Count == 0
                ? new KeratometryInput?[] { null }
                : availableInputs.Cast<KeratometryInput?>().ToList();
        }

        return combination.KeratometryInputs
            .Where(availableInputs.Contains)
            .Cast<KeratometryInput?>()
            .ToList();
    }

    private static bool HasValue(FieldId field, FormulaEyeSnapshot eye)
    {
        if (field == FieldId.PkDevice)
        {
            return eye.PkDevice.HasValue;
        }

        return eye.Values.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool MatchesStaticDimensions(FormulaCombination combination, FormulaEyeSnapshot eye)
    {
        return (!combination.Toric.HasValue || combination.Toric.Value == eye.IsToric)
            && Supports(combination.PostLvcRk, eye.PostLvcRk)
            && SupportsAny(combination.KeratometryInputs, eye.AvailableKeratometryInputs)
            && (!combination.Keratoconus.HasValue || combination.Keratoconus.Value == eye.IsKeratoconus)
            && (!combination.SosAL.HasValue || combination.SosAL.Value == eye.IsSosAL);
    }

    private static bool Matches(
        FormulaCondition condition,
        FormulaEyeSnapshot eye,
        FormulaCombinationContext context)
    {
        return (!condition.Toric.HasValue || condition.Toric.Value == eye.IsToric)
            && Supports(condition.PostLvcRk, eye.PostLvcRk)
            && SupportsConditionInput(condition.KeratometryInputs, context.KeratometryInput)
            && (!condition.Keratoconus.HasValue || condition.Keratoconus.Value == eye.IsKeratoconus)
            && (!condition.SosAL.HasValue || condition.SosAL.Value == eye.IsSosAL);
    }

    private static bool Supports<T>(IReadOnlyCollection<T> supportedValues, T actualValue)
        where T : struct, Enum
    {
        return supportedValues.Count == 0 || supportedValues.Contains(actualValue);
    }

    private static bool SupportsAny<T>(IReadOnlyCollection<T> supportedValues, IReadOnlyCollection<T> actualValues)
        where T : struct, Enum
    {
        return supportedValues.Count == 0 || actualValues.Any(supportedValues.Contains);
    }

    private static bool SupportsConditionInput(
        IReadOnlyCollection<KeratometryInput> conditionInputs,
        KeratometryInput? actualInput)
    {
        return conditionInputs.Count == 0 || actualInput.HasValue && conditionInputs.Contains(actualInput.Value);
    }

    private static string FormatSupportedPostLvc(IEnumerable<FormulaCombination> combinations)
    {
        var values = combinations
            .SelectMany(combination => combination.PostLvcRk)
            .Distinct()
            .OrderBy(value => value)
            .Select(FormatPostLvc)
            .ToList();

        return values.Count == 0 ? "any post-LVC/RK mode" : string.Join(", ", values);
    }

    private static string FormatSupportedKeratometryInputs(IEnumerable<FormulaCombination> combinations)
    {
        var values = combinations
            .SelectMany(combination => combination.KeratometryInputs)
            .Distinct()
            .OrderBy(value => value)
            .Select(FormatKeratometryInput)
            .ToList();

        return values.Count == 0 ? "any keratometry input" : string.Join(", ", values);
    }

    public static string FormatPostLvc(PostLvcRkType value) => value switch
    {
        PostLvcRkType.None => "standard eyes",
        PostLvcRkType.MLvc => "M-LVC",
        PostLvcRkType.HLvc => "H-LVC",
        PostLvcRkType.RK => "RK",
        _ => value.ToString()
    };

    public static string FormatPkDevice(PkDevice value) => value switch
    {
        PkDevice.IOLMaster700 => "IOLMaster 700",
        PkDevice.Pentacam => "Pentacam",
        _ => value.ToString()
    };

    public static string FormatKeratometryInput(KeratometryInput value) => value switch
    {
        KeratometryInput.EstimatedPK => "estimated PK",
        KeratometryInput.PentacamPK => "Pentacam PK",
        KeratometryInput.IOLMaster700PK => "IOLMaster 700 PK",
        KeratometryInput.IOLMaster700TK => "IOLMaster 700 TK",
        _ => value.ToString()
    };

    public static string FormatRange(FieldRangeDefinition range)
    {
        var bounds = (range.Min, range.Max) switch
        {
            ({ } min, { } max) => $"{min:0.##}-{max:0.##}",
            ({ } min, null) => $">= {min:0.##}",
            (null, { } max) => $"<= {max:0.##}",
            _ => "numeric"
        };

        return string.IsNullOrWhiteSpace(range.Unit) ? bounds : $"{bounds} {range.Unit}";
    }
}
