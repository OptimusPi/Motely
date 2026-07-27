using Motely.Filters.Jaml;

namespace Motely.Lsp.Core;

/// <summary>
/// The JAML language brain: diagnostics, hover, and completion computed directly off the
/// engine's own grammar — <c>JamlSchema</c> (generated from <c>[JamlDiscriminator]</c>),
/// <c>JamlConfigLoader</c> (the one true parser), and the engine's enums (the one true
/// vocabulary). Protocol-free on purpose: the stdio server and tests call these same methods,
/// so the grammar stays authored exactly once, in C#.
/// </summary>
public static class JamlLanguageService
{
    // ── Diagnostics ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse <paramref name="text"/> with the real engine loader and report what it reports.
    /// Every positioned failure carries its own <see cref="JamlSpan"/> straight from the engine —
    /// the tokenizer's for a syntax error, the rejected key's for an unknown-key error — so the
    /// squiggle lands on the offending token. A semantic error the loader can't yet place
    /// (a value outside its enum) falls back to the first line.
    /// </summary>
    public static IReadOnlyList<JamlDiagnostic> Diagnose(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        try
        {
            _ = JamlConfigLoader.FromJaml(text);
            return [];
        }
        catch (Exception ex)
        {
            return [ToDiagnostic(ex, text)];
        }
    }

    private static JamlDiagnostic ToDiagnostic(Exception ex, string text)
    {
        // Both positioned failures carry the span the parser/loader already held — walk the
        // inner chain (FromJaml can wrap) and paint the squiggle exactly where it belongs.
        for (Exception? walk = ex; walk is not null; walk = walk.InnerException)
        {
            if (walk is JamlSyntaxException syntax)
                return new JamlDiagnostic(
                    ClampSpan(syntax.Span, text),
                    syntax.RawMessage,
                    JamlDiagnosticSeverity.Error,
                    "JAML0001"
                );

            if (walk is JamlSemanticException semantic && !semantic.Span.IsEmpty)
                return new JamlDiagnostic(
                    semantic.Span,
                    semantic.Message,
                    JamlDiagnosticSeverity.Error,
                    "JAML0100"
                );
        }

        var firstLineLength = text.IndexOf('\n') is var nl && nl >= 0 ? nl : text.Length;
        return new JamlDiagnostic(
            JamlSpan.WholeLine(0, Math.Max(firstLineLength, 1)),
            ex.Message,
            JamlDiagnosticSeverity.Error,
            "JAML0100"
        );
    }

    private static JamlSpan ClampSpan(JamlSpan span, string text)
    {
        if (!span.IsEmpty)
            return span;
        var firstLineLength = text.IndexOf('\n') is var nl && nl >= 0 ? nl : text.Length;
        return JamlSpan.WholeLine(0, Math.Max(firstLineLength, 1));
    }

    // ── Hover ───────────────────────────────────────────────────────────────────────────

    /// <summary>Markdown for the word under the cursor, or null when there is nothing to say.</summary>
    public static JamlHoverInfo? Hover(string text, int line, int character)
    {
        var lines = SplitLines(text);
        if (line < 0 || line >= lines.Length)
            return null;
        var (word, span) = WordAt(lines[line], line, character);
        if (word.Length == 0)
            return null;

        if (IsDiscriminator(word))
        {
            var keys = JamlSchema.ClauseKeysFor(word);
            var md = $"**{word}** — JAML clause";
            var valueEnum = JamlSchema.ValueEnumTypeFor(word);
            if (valueEnum is not null)
                md += $"\n\nValue: `{valueEnum.Name}` ({Enum.GetNames(valueEnum).Length} names)";
            if (keys.Length > 0)
                md += $"\n\nKeys: {string.Join(", ", keys.Select(k => $"`{k}`"))}";
            if (JamlSchema.RollsAreInlineFor(word))
                md += "\n\nRolls event — the value is the roll list.";
            return new JamlHoverInfo(span, md);
        }

        foreach (var (enumType, kind) in JamlSchema.ValueEnumKinds)
            if (Enum.GetNames(enumType).FirstOrDefault(n =>
                    n.Equals(word, StringComparison.OrdinalIgnoreCase)) is { } exact)
                return new JamlHoverInfo(span, $"**{exact}** — {kind} (`{enumType.Name}`)");

        var context = ContextAt(lines, line);
        if (context.Discriminator is { } disc)
        {
            var keys = JamlSchema.ClauseKeysFor(disc);
            if (keys.Any(k => k.Equals(word, StringComparison.OrdinalIgnoreCase)))
                return new JamlHoverInfo(span, $"`{word}` — key of the **{disc}** clause");
        }

        return null;
    }

    // ── Explain (schema dump for tools / @jimbo /explain) ─────────────────────────────

    /// <summary>
    /// Markdown explanation of a JAML word from the engine schema — discriminators, clause keys,
    /// value enums, root keys, or vocabulary hits via <see cref="JamlSchema.ListItems"/>.
    /// Returns null when the topic is empty or unknown.
    /// </summary>
    public static string? Explain(string topic)
    {
        var q = topic.Trim();
        if (q.Length == 0)
            return null;

        // Bare kind: "joker" as discriminator vs listItems query "joker Blueprint"
        var parts = q.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        var head = parts[0];
        var rest = parts.Length > 1 ? parts[1].Trim() : null;

        if (IsDiscriminator(head))
        {
            var disc = JamlSchema.Discriminators.First(d =>
                d.Equals(head, StringComparison.OrdinalIgnoreCase)
            );
            var keys = JamlSchema.ClauseKeysFor(disc);
            var valueEnum = JamlSchema.ValueEnumTypeFor(disc);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**`{disc}`** — JAML clause discriminator (Motely engine schema)");
            sb.AppendLine();
            if (valueEnum is not null)
            {
                var names = Enum.GetNames(valueEnum);
                sb.AppendLine($"**Value enum:** `{valueEnum.Name}` ({names.Length} names + `Any`)");
                if (rest is { Length: > 0 })
                {
                    var hits = JamlSchema.ListItems(disc, rest);
                    if (hits.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"**Matches for `{rest}`:**");
                        foreach (var h in hits.Take(25))
                            sb.AppendLine($"- `{h}`");
                        if (hits.Length > 25)
                            sb.AppendLine($"- … +{hits.Length - 25} more");
                    }
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine("Sample values: " + string.Join(", ", names.Take(12).Select(n => $"`{n}`")));
                    if (names.Length > 12)
                        sb.AppendLine($"… +{names.Length - 12} more (ask e.g. `{disc} lucky`)");
                }
            }
            if (keys.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Clause keys:** " + string.Join(", ", keys.Select(k => $"`{k}`")));
            }
            if (JamlSchema.RollsAreInlineFor(disc))
            {
                sb.AppendLine();
                sb.AppendLine("Rolls event — the clause value is the roll list.");
            }
            var sources = JamlSchema.SourceKeysFor(disc);
            if (sources is { Length: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("**Source keys:** " + string.Join(", ", sources.Select(k => $"`{k}`")));
            }
            sb.AppendLine();
            sb.AppendLine(
                "**must** = hard filter · **should** = scoring · **mustNot** = forbid. One grammar: FilterDesc → JamlSchema."
            );
            return sb.ToString().TrimEnd();
        }

        if (JamlConfig.RootKeys.Any(k => k.Equals(head, StringComparison.OrdinalIgnoreCase)))
        {
            var key = JamlConfig.RootKeys.First(k => k.Equals(head, StringComparison.OrdinalIgnoreCase));
            return key switch
            {
                "must" =>
                    "**`must`** — hard requirements. Seed fails if any must clause misses. List of clauses under `- disc: …`.",
                "should" =>
                    "**`should`** — soft scoring clauses. Matches add score (see `score:`). Do not alone fail the seed unless cutoff logic says so.",
                "mustNot" =>
                    "**`mustNot`** — forbidden patterns. Seed fails if a mustNot clause matches.",
                "deck" =>
                    "**`deck`** — MotelyDeck (Red, Blue, … Erratic).",
                "stake" =>
                    "**`stake`** — MotelyStake (White … Gold).",
                "seeds" =>
                    "**`seeds`** — optional saved seed list for list search / lake.",
                "name" => "**`name`** — human filter title.",
                "antes" =>
                    "**`antes`** — optional root ante scope; many clauses also take per-clause `antes:`.",
                _ => $"**`{key}`** — JAML root key (engine `JamlConfig.RootKeys`).",
            };
        }

        // Enum vocabulary hit (joker name, voucher, …)
        foreach (var (enumType, kind) in JamlSchema.ValueEnumKinds)
        {
            var exact = Enum.GetNames(enumType)
                .FirstOrDefault(n => n.Equals(head, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
                return $"**`{exact}`** — {kind} (`{enumType.Name}`). Use under the matching clause discriminator.";
        }

        // ListItems kind + optional query — unknown kinds throw; treat as not found.
        try
        {
            if (rest is not null)
            {
                var items = JamlSchema.ListItems(head, rest);
                if (items.Length > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"**`{head}`** vocabulary matches for `{rest}` ({items.Length}):");
                    foreach (var h in items.Take(40))
                        sb.AppendLine($"- `{h}`");
                    if (items.Length > 40)
                        sb.AppendLine($"- … +{items.Length - 40} more");
                    return sb.ToString().TrimEnd();
                }
            }
            else
            {
                var items = JamlSchema.ListItems(head, null);
                if (items.Length > 0 && items.Length <= 80)
                {
                    return $"**`{head}`** — {items.Length} engine names:\n"
                        + string.Join("\n", items.Take(40).Select(i => $"- `{i}`"))
                        + (items.Length > 40 ? $"\n- … +{items.Length - 40} more" : "");
                }
                if (items.Length > 80)
                {
                    return $"**`{head}`** — {items.Length} engine names. Narrow with a query, e.g. `{head} lucky`.";
                }
            }
        }
        catch (ArgumentException)
        {
            // Unknown vocabulary kind — fall through to null.
        }

        return null;
    }

    // ── Completion ──────────────────────────────────────────────────────────────────────

    /// <summary>Completion candidates at the cursor, already filtered by the typed prefix.</summary>
    public static IReadOnlyList<JamlCompletionItem> Complete(string text, int line, int character)
    {
        var lines = SplitLines(text);
        var current = line >= 0 && line < lines.Length ? lines[line] : "";
        var prefix = current[..Math.Min(Math.Max(character, 0), current.Length)];

        var indent = CountIndent(prefix);
        var body = prefix.TrimStart();
        var isListItem = body.StartsWith("- ", StringComparison.Ordinal) || body == "-";
        if (isListItem)
            body = body.TrimStart('-').TrimStart();

        var colon = body.IndexOf(':');
        if (colon >= 0)
            return CompleteValue(lines, line, body[..colon].Trim(), body[(colon + 1)..].TrimStart());
        return CompleteKey(lines, line, indent, isListItem, body);
    }

    private static IReadOnlyList<JamlCompletionItem> CompleteValue(
        string[] lines, int line, string key, string valuePrefix)
    {
        // Discriminator value first (adds "Any" wildcard). Property/root keys second.
        if (IsDiscriminator(key) && JamlSchema.ValueEnumTypeFor(key) is { } valueEnum)
        {
            var names = Enum.GetNames(valueEnum).ToList();
            names.Add("Any");
            return FilterNames(names, valuePrefix, "value", valueEnum.Name);
        }

        if (JamlSchema.EnumTypeForKind(key) is { } keyEnum)
            return FilterNames(Enum.GetNames(keyEnum), valuePrefix, "value", keyEnum.Name);

        return [];
    }

    private static IReadOnlyList<JamlCompletionItem> CompleteKey(
        string[] lines, int line, int indent, bool isListItem, string typed)
    {
        if (indent == 0 && !isListItem)
            return FilterNames(JamlConfig.RootKeys, typed, "key", "JAML root key");

        var context = ContextAt(lines, line);

        if (isListItem && context.InClauseList)
            return FilterNames(
                JamlSchema.Discriminators.Distinct(StringComparer.OrdinalIgnoreCase),
                typed, "discriminator", "JAML clause");

        if (context.BlockKey is "sources" && context.Discriminator is { } srcDisc)
            return FilterNames(
                JamlSchema.SourceKeysFor(srcDisc) ?? [], typed, "key", $"{srcDisc} source key");

        if (context.BlockKey is "with")
            return FilterNames(JamlClause.WithBlockKeys, typed, "key", "with-block key");

        if (context.Discriminator is { } disc)
            return FilterNames(JamlSchema.ClauseKeysFor(disc), typed, "key", $"{disc} clause key");

        return [];
    }

    private static IReadOnlyList<JamlCompletionItem> FilterNames(
        IEnumerable<string> names, string typed, string kind, string detail)
    {
        var starts = new List<JamlCompletionItem>();
        var contains = new List<JamlCompletionItem>();
        foreach (var name in names)
        {
            if (typed.Length == 0 || name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                starts.Add(new JamlCompletionItem(name, kind, detail));
            else if (name.Contains(typed, StringComparison.OrdinalIgnoreCase))
                contains.Add(new JamlCompletionItem(name, kind, detail));
        }
        starts.AddRange(contains);
        return starts;
    }

    // ── Context detection ───────────────────────────────────────────────────────────────

    private readonly record struct LineContext(
        string? Discriminator,
        string? BlockKey,
        bool InClauseList
    );

    /// <summary>
    /// Walk upward from <paramref name="line"/> collecting the enclosing shape: the nearest
    /// block key at a lower indent (sources:, with:, clauses:), the clause's discriminator,
    /// and whether we're inside a must/should/mustNot list at all.
    /// </summary>
    private static LineContext ContextAt(string[] lines, int line)
    {
        string? discriminator = null;
        string? blockKey = null;
        var inClauseList = false;

        var reference = int.MaxValue;
        if (line >= 0 && line < lines.Length && lines[line].Trim().Length > 0)
            reference = EffectiveIndent(lines[line]);

        for (var i = Math.Min(line, lines.Length - 1); i >= 0; i--)
        {
            var raw = lines[i];
            if (raw.Trim().Length == 0)
                continue;
            var lineIndent = EffectiveIndent(raw);
            if (i != line && lineIndent > reference)
                continue;
            if (i != line)
                reference = lineIndent;

            var body = raw.TrimStart();
            var fromListItem = body.StartsWith("- ", StringComparison.Ordinal);
            if (fromListItem)
                body = body[2..].TrimStart();

            var colon = body.IndexOf(':');
            if (colon <= 0)
                continue;
            var key = body[..colon].Trim();

            if (discriminator is null && IsDiscriminator(key))
                discriminator = key;
            else if (blockKey is null && discriminator is null
                && (key is "sources" or "with" or "clauses"))
                blockKey = key;

            if (key is "must" or "should" or "mustNot")
            {
                inClauseList = true;
                break;
            }
        }

        return new LineContext(discriminator, blockKey, inClauseList);
    }

    private static int EffectiveIndent(string line)
    {
        var indent = CountIndent(line);
        return line.TrimStart().StartsWith('-') ? indent + 2 : indent;
    }

    private static bool IsDiscriminator(string word) =>
        JamlSchema.Discriminators.Any(d => d.Equals(word, StringComparison.OrdinalIgnoreCase));

    private static int CountIndent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
            count++;
        return count;
    }

    private static (string Word, JamlSpan Span) WordAt(string lineText, int line, int character)
    {
        if (character < 0)
            return ("", default);
        var at = Math.Min(character, Math.Max(lineText.Length - 1, 0));
        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
        if (lineText.Length == 0 || (!IsWordChar(lineText[at]) && (at == 0 || !IsWordChar(lineText[at - 1]))))
            return ("", default);
        if (!IsWordChar(lineText[at]))
            at--;
        var start = at;
        while (start > 0 && IsWordChar(lineText[start - 1]))
            start--;
        var end = at;
        while (end + 1 < lineText.Length && IsWordChar(lineText[end + 1]))
            end++;
        return (lineText[start..(end + 1)], JamlSpan.OnLine(line, start, end + 1 - start));
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');
}
