using System.Globalization;

namespace Motely.Filters.Jaml;

/// <summary>
/// <see cref="IJamlValueReader"/> for the JAML loader path.
/// Parse failures throw (same contract as <see cref="JamlConfigLoader.FromJaml"/>);
/// <c>false</c> means the value is absent, not invalid.
/// </summary>
internal sealed class JamlLoaderValueReader : IJamlValueReader
{
    private readonly string _text;
    private readonly string[]? _array;
    private readonly bool _hasArray;

    private JamlLoaderValueReader(string text, string[]? array, bool hasArray, JamlSpan span)
    {
        _text = text;
        _array = array;
        _hasArray = hasArray;
        Span = span;
    }

    public string Text => _text;
    public JamlSpan Span { get; }
    public bool IsAny => string.Equals(_text, "any", StringComparison.OrdinalIgnoreCase);

    public static JamlLoaderValueReader FromScalar(string? text, JamlSpan span = default) =>
        new(text ?? "", null, false, span);

    public static JamlLoaderValueReader FromStrings(string[]? values, JamlSpan span = default)
    {
        if (values is null || values.Length == 0)
            return new("", null, false, span);
        if (values.Length == 1)
            return new(values[0], values, true, span);
        return new(string.Join(", ", values), values, true, span);
    }

    public bool TryInt(out int value)
    {
        if (int.TryParse(_text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        value = 0;
        return false;
    }

    public bool TryBool(out bool value)
    {
        if (bool.TryParse(_text, out value))
            return true;
        if (string.Equals(_text, "yes", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }
        if (string.Equals(_text, "no", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }
        value = false;
        return false;
    }

    public bool TryIntArray(out int[] value)
    {
        if (string.IsNullOrWhiteSpace(_text) && !(_hasArray && _array is { Length: > 0 }))
        {
            value = [];
            return false;
        }

        if (int.TryParse(_text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var one)
            && !(_hasArray && _array is { Length: > 1 }))
        {
            value = [one];
            return true;
        }

        if (_hasArray && _array is not null)
        {
            var list = new int[_array.Length];
            for (int i = 0; i < _array.Length; i++)
            {
                if (!int.TryParse(_array[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out list[i]))
                    throw new JamlSemanticException($"Cannot parse '{_array[i]}' as int.", Span);
            }
            value = list;
            return true;
        }

        value = [];
        return false;
    }

    public bool TryEnum<TEnum>(out TEnum value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(_text))
        {
            value = default;
            return false;
        }

        if (typeof(TEnum) == typeof(MotelyStandardcardRank))
        {
            value = (TEnum)(object)ParseRank(_text);
            return true;
        }

        value = ParseEnum<TEnum>(_text);
        return true;
    }

    public bool TryEnumArray<TEnum>(out TEnum[] value) where TEnum : struct, Enum
    {
        var parts = _hasArray && _array is { Length: > 0 }
            ? _array
            : (string.IsNullOrWhiteSpace(_text) ? null : new[] { _text });
        if (parts is null)
        {
            value = [];
            return false;
        }

        value = new TEnum[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (typeof(TEnum) == typeof(MotelyStandardcardRank))
                value[i] = (TEnum)(object)ParseRank(parts[i]);
            else
                value[i] = ParseEnum<TEnum>(parts[i]);
        }
        return true;
    }

    private TEnum ParseEnum<TEnum>(string raw) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed))
            return parsed;

        var normalized = raw
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);
        if (Enum.TryParse<TEnum>(normalized, ignoreCase: true, out parsed))
            return parsed;

        throw new JamlSemanticException(JamlEnumMessages.CannotParse(raw, typeof(TEnum)), Span);
    }

    private MotelyStandardcardRank ParseRank(string value)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pip))
        {
            return pip switch
            {
                2 => MotelyStandardcardRank.Two,
                3 => MotelyStandardcardRank.Three,
                4 => MotelyStandardcardRank.Four,
                5 => MotelyStandardcardRank.Five,
                6 => MotelyStandardcardRank.Six,
                7 => MotelyStandardcardRank.Seven,
                8 => MotelyStandardcardRank.Eight,
                9 => MotelyStandardcardRank.Nine,
                10 => MotelyStandardcardRank.Ten,
                _ => throw new JamlSemanticException($"Unsupported rank pip value: {pip}.", Span),
            };
        }

        return value.ToUpperInvariant() switch
        {
            "J" => MotelyStandardcardRank.Jack,
            "Q" => MotelyStandardcardRank.Queen,
            "K" => MotelyStandardcardRank.King,
            "A" => MotelyStandardcardRank.Ace,
            _ => ParseEnum<MotelyStandardcardRank>(value),
        };
    }
}
