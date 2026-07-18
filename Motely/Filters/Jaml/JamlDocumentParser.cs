namespace Motely.Filters.Jaml;

// JAML's own document tree — no YAML library, no YAML semantics borrowed. A JAML document is a
// small, fixed shape (root key/value pairs; must/should/mustNot lists of either a nested
// key/value clause block or a one-line clause string; flow arrays like [0,1,2]; nested
// sources:/with:/clauses: blocks) — small enough that JAML parses itself, the same way
// JamlLine.cs already parses one clause line without borrowing anyone else's grammar.

internal abstract class JNode
{
    // Where this node sits in the source. The parser knows every position as it walks the lines;
    // keeping it here is what lets a diagnostic point at the character instead of the document.
    // Nodes the writer builds from scratch leave this default (IsEmpty) — they have no source.
    public JamlSpan Span { get; init; }
}

internal sealed class JMap : JNode
{
    // Insertion order preserved (JamlConfigWriter round-trips in a stable order); lookups are
    // case-insensitive, matching every wire key in the grammar (camelCase, but authors mistype
    // case sometimes and the loader has always tolerated it).
    private readonly Dictionary<string, JNode> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JamlSpan> _keySpans = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = [];

    public IReadOnlyList<string> Keys => _order;

    // The key's own span is required, and tracked separately from the value's: "unknown key
    // 'jokerz'" has to underline jokerz, while the value node's span points at whatever came
    // after the colon. No convenience overload that defaults it — a document where some keys
    // know where they are and some don't is worse than one where none do, because nothing tells
    // you which is which.
    public void Set(string key, JNode value, JamlSpan keySpan)
    {
        if (!_items.ContainsKey(key))
            _order.Add(key);
        _items[key] = value;
        _keySpans[key] = keySpan;
    }

    public JNode? Get(string key) => _items.TryGetValue(key, out var v) ? v : null;

    /// <summary>The span of the key itself as written, or an empty span for a synthesized node.</summary>
    public JamlSpan KeySpan(string key) => _keySpans.TryGetValue(key, out var s) ? s : default;
}

internal sealed class JSeq : JNode
{
    public List<JNode> Items { get; } = [];
}

// Holds the parsed text alongside what KIND of literal it looked like on the page — an integer,
// a bare word, or something that needed quotes to disambiguate from either. The writer uses this
// to decide whether a value needs quoting by asking what it IS, not by re-deriving it from a pile
// of Contains(':')/StartsWith('-') heuristics after the fact. The reader still hands everything to
// callers as text (GetInt/GetBool re-parse it) — JAML's wire format is text; this only stops the
// writer from throwing typing away and immediately having to guess it back.
internal enum JScalarKind { Bare, Quoted, Integer }

internal sealed class JScalar(string value, JScalarKind kind = JScalarKind.Bare) : JNode
{
    public string Value { get; } = value;
    public JScalarKind Kind { get; } = kind;

    public static JScalar Of(int value) => new(value.ToString(), JScalarKind.Integer);
    public static JScalar Of(bool value) => new(value ? "true" : "false");
}

/// <summary>Thrown with a line number so a parse failure points at the actual offending line,
/// the same way the old SharpYaml errors did (Zerkeo.jaml's "mapping values not allowed" case
/// this session) — losing that would be a real regression, not just a cosmetic one.
/// The position is kept as data as well as formatted into the message, so an editor can draw a
/// squiggle on the offending line rather than regex it back out of the text.</summary>
internal sealed class JamlSyntaxException(string message, JamlSpan span)
    : Exception($"JAML parse error at line {span.StartLine + 1}: {message}")
{
    /// <summary>The message without the "JAML parse error at line N:" preamble.</summary>
    public string RawMessage { get; } = message;

    /// <summary>Where the failure sits. Required — every throw site is holding the line it failed
    /// on, so there is no such thing as a parse error that doesn't know where it happened.</summary>
    public JamlSpan Span { get; } = span;
}

internal static class JamlDocumentParser
{
    // ── JAML-native block format (the human-authored .jaml files) ──────────────────────────

    public static JMap ParseJaml(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        int i = 0;
        var root = ParseBlock(lines, ref i, 0);
        if (root is not JMap map)
            throw new JamlSyntaxException("JAML root must be a mapping.", JamlSpan.OfLine(0, lines.Length > 0 ? lines[0] : ""));
        return map;
    }

    // Parses the block starting at `i` whose lines are indented at exactly `indent` — either a
    // sequence of "- " items or a sequence of "key: value" mappings. Which one is decided by the
    // first non-blank line's shape; a real JAML document never mixes the two at one indent level.
    private static JNode ParseBlock(string[] lines, ref int i, int indent)
    {
        SkipBlankAndComments(lines, ref i);
        if (i >= lines.Length || IndentOf(lines[i]) < indent)
            return new JMap(); // an empty block (e.g. `with:` with nothing under it) is an empty map

        string head = lines[i].AsSpan(IndentOf(lines[i])).TrimStart().ToString();
        if (head.StartsWith('['))
            return ParseMultilineFlowArray(lines, ref i, "");
        return head.StartsWith('-') ? ParseSequence(lines, ref i, indent) : ParseMapping(lines, ref i, indent);
    }

    private static JMap ParseMapping(string[] lines, ref int i, int indent)
    {
        var map = new JMap();
        while (true)
        {
            SkipBlankAndComments(lines, ref i);
            if (i >= lines.Length)
                break;
            int lineIndent = IndentOf(lines[i]);
            if (lineIndent < indent)
                break;
            if (lineIndent > indent)
                throw new JamlSyntaxException($"Unexpected indent (expected {indent} spaces).", JamlSpan.OfLine(i, lines[i]));

            var (key, inlineValue, keySpan) = SplitKeyValue(lines[i], i);
            int keyLineIndent = lineIndent;
            i++;

            if (inlineValue is "|" or ">" or "|-" or ">-" or "|+" or ">+")
            {
                map.Set(key, new JScalar(ParseBlockScalar(lines, ref i, keyLineIndent, inlineValue[0])), keySpan);
            }
            else if (inlineValue is { Length: > 0 } && inlineValue.StartsWith('[') && !inlineValue.EndsWith(']'))
            {
                // "key: [" opens a flow array inline but doesn't close it on this line — the
                // items and the closing "]" continue on following lines (real corpus files write
                // long shopItems:/seeds: arrays this way for readability). `i` already points at
                // the line right after the key line; the collector picks up from there.
                map.Set(key, ParseMultilineFlowArray(lines, ref i, inlineValue), keySpan);
            }
            else if (inlineValue is { Length: > 0 })
            {
                map.Set(key, ParseScalarOrFlow(inlineValue, i - 1), keySpan);
            }
            else
            {
                // "key:" with nothing after it — the value is a nested block. Normally deeper-
                // indented than the key; a block SEQUENCE or a flow array's opening "[" is also
                // legal at the SAME indent as its own key (real corpus files use both — "- or:\n
                // - erraticSuit: Hearts" with the nested list at the same column as "or:" itself).
                // A same-indent MAPPING would be genuinely ambiguous with the parent's own sibling
                // keys, so only a same-indent "-" or "[" start gets this exception.
                SkipBlankAndComments(lines, ref i);
                int childIndent = i < lines.Length ? IndentOf(lines[i]) : -1;
                bool sameIndentBlockStart =
                    childIndent == indent
                    && i < lines.Length
                    && lines[i].AsSpan(indent).TrimStart() is { Length: > 0 } head
                    && (head[0] == '-' || head[0] == '[');
                map.Set(
                    key,
                    childIndent > indent || sameIndentBlockStart
                        ? ParseBlock(lines, ref i, childIndent)
                        : new JMap(),
                    keySpan
                );
            }
        }
        return map;
    }

    private static JSeq ParseSequence(string[] lines, ref int i, int indent)
    {
        var seq = new JSeq();
        while (true)
        {
            SkipBlankAndComments(lines, ref i);
            if (i >= lines.Length)
                break;
            int lineIndent = IndentOf(lines[i]);
            if (lineIndent < indent)
                break;
            if (lineIndent > indent)
                throw new JamlSyntaxException($"Unexpected indent (expected {indent} spaces).", JamlSpan.OfLine(i, lines[i]));

            string trimmed = lines[i].AsSpan(lineIndent).ToString();
            if (!trimmed.StartsWith('-'))
            {
                // Not a list item at this indent — if this sequence IS a same-indent value of a
                // mapping key (see ParseBlock/ParseMapping's sameIndentBlockStart), this line is
                // actually the next SIBLING KEY of that same mapping, not part of the sequence
                // ("- or: [...]\n  min: 13" — "min" belongs next to "or", not inside its list).
                // Return what's collected so far and let the caller's own mapping loop re-examine
                // this line; it validates as a real key there, or fails there with a clear error.
                break;
            }

            // The list item's own content starts right after "- ", at a virtual indent equal to
            // where that content begins — this is what lets "- joker: Blueprint" open a mapping
            // whose later sibling keys ("  min: 1") are indented to align under the item content,
            // not under the dash.
            string afterDash = trimmed.Length > 1 ? trimmed[1..] : "";
            int contentCol = lineIndent + (trimmed.Length - afterDash.TrimStart().Length);
            string content = afterDash.TrimStart();

            if (content.Length == 0)
            {
                // "- " alone, value entirely on following indented lines.
                i++;
                seq.Items.Add(ParseBlock(lines, ref i, indent + 1));
                continue;
            }

            if (LooksLikeKeyValue(content))
            {
                // Rewrite this line as if it were a plain mapping line at contentCol, then let
                // ParseMapping consume it and any deeper-indented sibling keys that follow.
                lines[i] = new string(' ', contentCol) + content;
                seq.Items.Add(ParseMapping(lines, ref i, contentCol));
            }
            else
            {
                // A bare scalar list item — a one-line JAML clause ("Blueprint in ante 1"), a
                // plain string (a seeds: entry), or a flow array token — JamlLine/ScalarValue
                // consumers decide which. The tokenizer's only job is not to lose the text.
                seq.Items.Add(new JScalar(content));
                i++;
            }
        }
        return seq;
    }

    // A very small, deliberately permissive rule: "key: value" only when the FIRST colon is
    // followed by whitespace-or-end-of-line, so a one-line clause like "Perkeo score 100" (no
    // colon at all) or a seed string never gets misread as a mapping key.
    private static bool LooksLikeKeyValue(string content)
    {
        int idx = content.IndexOf(':');
        if (idx < 0)
            return false;
        return idx + 1 == content.Length || char.IsWhiteSpace(content[idx + 1]);
    }

    // Also reports where the key itself sits on the line. "Unknown key 'jokerz'" has to underline
    // jokerz — the column is free here (it's just how far in the text was trimmed) and impossible
    // to recover later, once the key is a bare string detached from its line.
    private static (string Key, string? Value, JamlSpan KeySpan) SplitKeyValue(string line, int lineIndex)
    {
        string uncommented = StripComment(line);
        string trimmed = uncommented.TrimStart();
        int keyColumn = uncommented.Length - trimmed.Length;
        int idx = trimmed.IndexOf(':');
        if (idx < 0 || (idx + 1 < trimmed.Length && !char.IsWhiteSpace(trimmed[idx + 1])))
            throw new JamlSyntaxException(
                $"Expected 'key: value' or 'key:', got '{trimmed}'.",
                JamlSpan.OnLine(lineIndex, keyColumn, trimmed.TrimEnd().Length));
        string key = trimmed[..idx].Trim();
        string value = trimmed[(idx + 1)..].Trim();
        // The key as written, before Trim() collapsed any padding between it and the colon.
        int keyLength = trimmed[..idx].TrimEnd().Length;
        return (key, value.Length == 0 ? null : value, JamlSpan.OnLine(lineIndex, keyColumn, keyLength));
    }

    // Collects a flow array "[a, b, c]" that spans multiple lines — either "key: [" left open at
    // end of line (leadingText carries what followed the "["), or "key:" with the "[" starting
    // fresh on its own following line (leadingText is ""). `i` points at the first unconsumed
    // line either way. Consumes lines until one contains the matching "]", joins everything
    // between the brackets, and splits on commas — same token handling as a single-line flow
    // array, just gathered across lines first.
    private static JSeq ParseMultilineFlowArray(string[] lines, ref int i, string leadingText)
    {
        var sb = new System.Text.StringBuilder(leadingText);
        while (!sb.ToString().Contains(']'))
        {
            if (i >= lines.Length)
                throw new JamlSyntaxException("Unterminated flow array (missing ']').", JamlSpan.OfLine(i - 1, lines[i - 1]));
            sb.Append(' ').Append(StripComment(lines[i]).Trim());
            i++;
        }
        string joined = sb.ToString();
        int open = joined.IndexOf('[');
        int close = joined.IndexOf(']');
        string inner = joined[(open + 1)..close];

        var seq = new JSeq();
        if (inner.Trim().Length > 0)
            foreach (var token in inner.Split(','))
            {
                string t = token.Trim();
                if (t.Length == 0)
                    continue;
                seq.Items.Add(
                    int.TryParse(t, out _) ? new JScalar(t, JScalarKind.Integer) : new JScalar(t)
                );
            }
        return seq;
    }

    // Strips a trailing "# comment" from a line of real content — real corpus files annotate
    // values inline ("luckymoney: 4 # 1x luck normal ..."). Only strips a '#' that starts a new
    // token (preceded by start-of-line or whitespace), so a value that legitimately contains '#'
    // isn't mistaken for one — none in this grammar do, but the check costs nothing.
    private static string StripComment(string line)
    {
        for (int j = 0; j < line.Length; j++)
        {
            if (line[j] == '#' && (j == 0 || char.IsWhiteSpace(line[j - 1])))
                return line[..j];
        }
        return line;
    }

    // "key: >" (folded — lines join with spaces) or "key: |" (literal — lines keep their own
    // newlines): every following line indented deeper than the key's own indent belongs to the
    // block; the first line's indent sets the block's left margin, stripped from every line.
    // Real corpus files use this for description: text that wraps across multiple lines — a
    // plain "key: value" line can't hold that, so JAML honors the same block-scalar indicator
    // a human reaches for out of YAML habit, same as JamlLine already tolerates quoted scalars.
    private static string ParseBlockScalar(string[] lines, ref int i, int keyIndent, char style)
    {
        var content = new List<string>();
        int? blockIndent = null;
        while (i < lines.Length)
        {
            string raw = lines[i];
            if (raw.Trim().Length == 0)
            {
                content.Add("");
                i++;
                continue;
            }
            int lineIndent = IndentOf(raw);
            if (lineIndent <= keyIndent)
                break;
            blockIndent ??= lineIndent;
            content.Add(raw[Math.Min(blockIndent.Value, raw.Length)..]);
            i++;
        }
        // Trim the trailing blank lines a block naturally accumulates at its end (the next key's
        // dedent stops the loop one line after the real content).
        while (content.Count > 0 && content[^1].Length == 0)
            content.RemoveAt(content.Count - 1);
        return style == '>' ? string.Join(" ", content).Trim() : string.Join("\n", content);
    }

    // A scalar value, or a flow array "[a, b, c]" — JAML's one and only bracket syntax. No flow
    // mappings, no multi-line block scalars: the real corpus never uses them, and JUMMY's one-line
    // clause tail already covers the case (a whole clause on one line) that would otherwise tempt
    // someone to reach for a fancier YAML form.
    private static JNode ParseScalarOrFlow(string text, int lineIndex)
    {
        text = text.Trim();
        // "{}" — an explicit empty mapping. Real meaning: "sources: {}" overrides with
        // match-nowhere, distinct from an absent sources: key (use engine defaults) — the loader
        // tells the two apart by whether GetObject("sources") returns a map at all, so "{}" must
        // become a real (empty) JMap here, not fall through to a bare scalar string "{}".
        if (text == "{}")
            return new JMap();
        if (text.StartsWith('[') && text.EndsWith(']'))
        {
            var seq = new JSeq();
            string inner = text[1..^1];
            if (inner.Trim().Length > 0)
                foreach (var token in inner.Split(','))
                    seq.Items.Add(new JScalar(token.Trim()));
            return seq;
        }
        // Strip a matching pair of quotes if present — JAML authors sometimes quote scalars out
        // of YAML habit; there's no escaping grammar to honor beyond that.
        if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
            text = text[1..^1];
        return new JScalar(text);
    }

    private static int IndentOf(string line)
    {
        int i = 0;
        while (i < line.Length && line[i] == ' ')
            i++;
        return i;
    }

    private static void SkipBlankAndComments(string[] lines, ref int i)
    {
        while (i < lines.Length)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                i++;
                continue;
            }
            break;
        }
    }

    // ── Real JSON (the WASM/API JSON entry point — genuinely different grammar from JAML's
    // own indentation-based format, so it gets its own small recursive-descent reader rather
    // than being coerced through the line-oriented parser above). ─────────────────────────────

    // The JSON reader walks a character offset rather than lines, so it converts back to a
    // line/column here. Every JSON error used to report line 0 no matter where it happened —
    // an editor drew every squiggle on the first line of the file. Errors are rare enough that
    // counting newlines at throw time costs nothing worth measuring.
    private static JamlSpan JsonSpan(string text, int index, int length = 1)
    {
        int clamped = Math.Clamp(index, 0, Math.Max(text.Length - 1, 0));
        int line = 0, lineStart = 0;
        for (int k = 0; k < clamped; k++)
            if (text[k] == '\n')
            {
                line++;
                lineStart = k + 1;
            }
        return JamlSpan.OnLine(line, clamped - lineStart, length);
    }

    public static JMap ParseJson(string text)
    {
        int i = 0;
        SkipJsonWhitespace(text, ref i);
        var node = ParseJsonValue(text, ref i);
        SkipJsonWhitespace(text, ref i);
        if (i != text.Length)
            throw new JamlSyntaxException("Trailing content after JSON document.", JsonSpan(text, i));
        if (node is not JMap map)
            throw new JamlSyntaxException("JSON root must be an object.", JsonSpan(text, 0));
        return map;
    }

    private static JNode ParseJsonValue(string text, ref int i)
    {
        SkipJsonWhitespace(text, ref i);
        if (i >= text.Length)
            throw new JamlSyntaxException("Unexpected end of JSON.", JsonSpan(text, i));
        return text[i] switch
        {
            '{' => ParseJsonObject(text, ref i),
            '[' => ParseJsonArray(text, ref i),
            '"' => new JScalar(ParseJsonString(text, ref i)),
            _ => ParseJsonLiteral(text, ref i),
        };
    }

    private static JMap ParseJsonObject(string text, ref int i)
    {
        var map = new JMap();
        i++; // '{'
        SkipJsonWhitespace(text, ref i);
        if (i < text.Length && text[i] == '}')
        {
            i++;
            return map;
        }
        while (true)
        {
            SkipJsonWhitespace(text, ref i);
            if (i >= text.Length || text[i] != '"')
                throw new JamlSyntaxException("Expected a JSON object key.", JsonSpan(text, i));
            int keyStart = i;
            string key = ParseJsonString(text, ref i);
            var keySpan = JsonSpan(text, keyStart, i - keyStart);
            SkipJsonWhitespace(text, ref i);
            if (i >= text.Length || text[i] != ':')
                throw new JamlSyntaxException("Expected ':' after JSON object key.", JsonSpan(text, i));
            i++;
            map.Set(key, ParseJsonValue(text, ref i), keySpan);
            SkipJsonWhitespace(text, ref i);
            if (i < text.Length && text[i] == ',')
            {
                i++;
                continue;
            }
            if (i < text.Length && text[i] == '}')
            {
                i++;
                break;
            }
            throw new JamlSyntaxException("Expected ',' or '}' in JSON object.", JsonSpan(text, i));
        }
        return map;
    }

    private static JSeq ParseJsonArray(string text, ref int i)
    {
        var seq = new JSeq();
        i++; // '['
        SkipJsonWhitespace(text, ref i);
        if (i < text.Length && text[i] == ']')
        {
            i++;
            return seq;
        }
        while (true)
        {
            seq.Items.Add(ParseJsonValue(text, ref i));
            SkipJsonWhitespace(text, ref i);
            if (i < text.Length && text[i] == ',')
            {
                i++;
                continue;
            }
            if (i < text.Length && text[i] == ']')
            {
                i++;
                break;
            }
            throw new JamlSyntaxException("Expected ',' or ']' in JSON array.", JsonSpan(text, i));
        }
        return seq;
    }

    private static string ParseJsonString(string text, ref int i)
    {
        i++; // opening quote
        var sb = new System.Text.StringBuilder();
        while (i < text.Length && text[i] != '"')
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                i++;
                sb.Append(text[i] switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    '/' => '/',
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    'b' => '\b',
                    'f' => '\f',
                    'u' => ParseJsonUnicodeEscape(text, ref i),
                    var other => other,
                });
            }
            else
            {
                sb.Append(c);
            }
            i++;
        }
        if (i >= text.Length)
            throw new JamlSyntaxException("Unterminated JSON string.", JsonSpan(text, i));
        i++; // closing quote
        return sb.ToString();
    }

    private static char ParseJsonUnicodeEscape(string text, ref int i)
    {
        // i is at 'u'; the 4 hex digits follow.
        string hex = text.Substring(i + 1, 4);
        i += 4;
        return (char)Convert.ToInt32(hex, 16);
    }

    // true/false/null/numbers all round-trip through JScalar as their literal text — every
    // downstream consumer (GetInt/GetBool/GetString) already parses text, same as the JAML path.
    private static JScalar ParseJsonLiteral(string text, ref int i)
    {
        int start = i;
        while (i < text.Length && text[i] != ',' && text[i] != '}' && text[i] != ']' && !char.IsWhiteSpace(text[i]))
            i++;
        if (i == start)
            throw new JamlSyntaxException($"Unexpected character '{text[i]}' in JSON.", JsonSpan(text, i));
        return new JScalar(text[start..i]);
    }

    private static void SkipJsonWhitespace(string text, ref int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
            i++;
    }
}
