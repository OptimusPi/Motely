using System.Reflection;
using System.Text;

namespace Motely.Filters.Jaml;

    // The write-side mirror of JamlClausePopulator: turns a JamlConfig back into JAML text using the
    // exact same ClauseKeys/SourceKeys reflection the loader reads, so a round trip through
    // FromJaml(ToJaml(config)) reproduces the same clause data. Not guaranteed to reproduce the
// original TEXT (e.g. "smallBlindTag"/"bigBlindTag" both come back out as "tag" with an explicit
// rolls: block; erraticRanks shorthand comes back out as "or" — both are real, parseable,
// semantically-identical JAML) — this is a data round trip, not a byte-for-byte one.
public static partial class JamlConfigLoader
{
    // The canonical discriminator per clause type is derived from JamlDiscriminatorRegistry.Entries
    // ITSELF — first-registered-wins, in the registry's own declared order — not a hand-typed
    // parallel list. A hand-typed list drifts the moment a discriminator is added to the registry
    // and someone forgets to also add it here (this happened: 9 plural-form discriminators were
    // missing from the old list). There is now exactly one place a discriminator is enumerated.
    private static readonly Dictionary<Type, string> CanonicalDiscriminatorByClauseType = BuildCanonicalDiscriminatorMap();

    private static Dictionary<Type, string> BuildCanonicalDiscriminatorMap()
    {
        var map = new Dictionary<Type, string>();
        foreach (var (discriminator, entry) in JamlDiscriminatorRegistry.Entries)
        {
            if (!map.ContainsKey(entry.ClauseType))
                map[entry.ClauseType] = discriminator;
        }
        return map;
    }

    // Item-value array property per discriminator family — the write-side twin of the
    // Jokers/Tarots/Spectrals/etc. assignments that stay bespoke on the read side (per family,
    // not reflected, since the property name isn't a convention-derivable function of the key).
    private static readonly Dictionary<string, string> ItemArrayPropertyByDiscriminator =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["joker"] = "Jokers",
            ["commonJoker"] = "Jokers",
            ["uncommonJoker"] = "Jokers",
            ["rareJoker"] = "Jokers",
            ["legendaryJoker"] = "Jokers",
            ["voucher"] = "Vouchers",
            ["tarotCard"] = "Tarots",
            ["spectralCard"] = "Spectrals",
            ["planetCard"] = "Planets",
            ["boss"] = "Bosses",
            ["tag"] = "Tags",
        };

    public static string ToJaml(JamlConfig config)
    {
        var root = new JMap();
        root.Set("id", new JScalar(config.Id), default);
        if (config.Name != null)
            root.Set("name", new JScalar(config.Name), default);
        if (config.Description != null)
            root.Set("description", new JScalar(config.Description), default);
        if (config.Author != null)
            root.Set("author", new JScalar(config.Author), default);
        if (config.Deck != MotelyDeck.Red)
            root.Set("deck", new JScalar(config.Deck.ToString()), default);
        if (config.Stake != MotelyStake.White)
            root.Set("stake", new JScalar(config.Stake.ToString()), default);
        if (config.Seeds.Count > 0)
            root.Set("seeds", StringArrayNode(config.Seeds), default);
        if (config.Filter is { Length: > 0 })
            root.Set("filter", new JScalar(config.Filter), default);
        if (config.Must.Count > 0)
            root.Set("must", ClauseListNode(config.Must), default);
        if (config.Should.Count > 0)
            root.Set("should", ClauseListNode(config.Should), default);
        if (config.MustNot.Count > 0)
            root.Set("mustNot", ClauseListNode(config.MustNot), default);

        var sb = new StringBuilder();
        WriteMap(sb, root, 0);
        return sb.ToString();
    }

    // ── JAML text emission — writes the same indented block format the native parser reads,
    // so ToJaml(config) round-trips through FromJaml unchanged. No third-party writer: the tree
    // built above (JMap/JSeq/JScalar) is JAML's own, so JAML also owns writing it back out. ─────

    private static void WriteMap(StringBuilder sb, JMap map, int indent)
    {
        foreach (var key in map.Keys)
        {
            var value = map.Get(key)!;
            WriteKeyed(sb, key, value, indent);
        }
    }

    private static void WriteKeyed(StringBuilder sb, string key, JNode value, int indent)
    {
        string pad = new(' ', indent);
        switch (value)
        {
            case JScalar scalar:
                sb.Append(pad).Append(key).Append(": ").Append(ScalarText(scalar)).Append('\n');
                break;
            case JMap { Keys.Count: 0 }:
                sb.Append(pad).Append(key).Append(": {}\n");
                break;
            case JMap childMap:
                sb.Append(pad).Append(key).Append(":\n");
                WriteMap(sb, childMap, indent + 2);
                break;
            case JSeq seq when IsFlowArray(seq):
                sb.Append(pad).Append(key).Append(": [")
                    .Append(string.Join(", ", seq.Items.Select(i => ScalarText((JScalar)i))))
                    .Append("]\n");
                break;
            case JSeq seq:
                sb.Append(pad).Append(key).Append(":\n");
                WriteSequence(sb, seq, indent);
                break;
        }
    }

    // Plain scalar lists (seeds:, flat int/string arrays like antes:/rolls:/shopItems:) stay
    // inline as "[a, b, c]" — only a sequence of clause mappings gets the multi-line "- " form.
    private static bool IsFlowArray(JSeq seq) => seq.Items.All(i => i is JScalar);

    private static void WriteSequence(StringBuilder sb, JSeq seq, int indent)
    {
        string pad = new(' ', indent);
        int itemIndent = indent + 2; // where every key of this list item aligns, "- " included

        foreach (var item in seq.Items)
        {
            if (item is not JMap itemMap)
                throw new InvalidOperationException("ToJaml: expected a clause mapping in a block sequence.");
            var keys = itemMap.Keys;
            if (keys.Count == 0)
            {
                sb.Append(pad).Append("- {}\n");
                continue;
            }
            // The first key's "key: value" text rides the "- " line itself (no separate pad —
            // "- " already occupies that column); any NESTED content the first key's own value
            // carries (a sub-clause list under "or:", a mapping under "sources:") must still
            // align at itemIndent like every other key here, not at column 0 — that was the bug:
            // a nested "or:" clause's children printed at indent 0 and reparsed as separate
            // top-level clauses instead of staying nested inside the "or:".
            string firstKey = keys[0];
            var firstValue = itemMap.Get(firstKey)!;
            sb.Append(pad).Append("- ");
            WriteKeyedInline(sb, firstKey, firstValue, itemIndent);

            foreach (var key in keys.Skip(1))
                WriteKeyed(sb, key, itemMap.Get(key)!, itemIndent);
        }
    }

    // Same cases as WriteKeyed, but for the key riding a "- " line: the "key:" text itself has no
    // leading pad (the caller already wrote "- "), while anything nested under it uses `indent`
    // as its real column, exactly like a normal (non-first) key at that list item's indent would.
    private static void WriteKeyedInline(StringBuilder sb, string key, JNode value, int indent)
    {
        switch (value)
        {
            case JScalar scalar:
                sb.Append(key).Append(": ").Append(ScalarText(scalar)).Append('\n');
                break;
            case JMap { Keys.Count: 0 }:
                sb.Append(key).Append(": {}\n");
                break;
            case JMap childMap:
                sb.Append(key).Append(":\n");
                WriteMap(sb, childMap, indent + 2);
                break;
            case JSeq seq when IsFlowArray(seq):
                sb.Append(key).Append(": [")
                    .Append(string.Join(", ", seq.Items.Select(i => ScalarText((JScalar)i))))
                    .Append("]\n");
                break;
            case JSeq seq:
                sb.Append(key).Append(":\n");
                WriteSequence(sb, seq, indent);
                break;
        }
    }

    private static JSeq ClauseListNode(IEnumerable<IJamlClause> clauses)
    {
        var seq = new JSeq();
        foreach (var clause in clauses)
            seq.Items.Add(WriteClause(clause));
        return seq;
    }

    private static JMap WriteClause(IJamlClause clause)
    {
        if (clause is LogicClause logic)
        {
            var logicDiscriminator = logic is AndClause ? "and" : "or";
            var logicMapping = new JMap();
            logicMapping.Set(logicDiscriminator, ClauseListNode(logic.Clauses), default);
            WriteCommonKeys(logicMapping, logic);
            return logicMapping;
        }

        var type = clause.GetType();
        if (!CanonicalDiscriminatorByClauseType.TryGetValue(type, out var discriminator))
            throw new InvalidOperationException($"ToJaml: no discriminator registered for clause type '{type.Name}'.");
        var entry = JamlDiscriminatorRegistry.Entries[discriminator];

        var mapping = new JMap();
        mapping.Set(discriminator, DiscriminatorValueNode(discriminator, entry, clause), default);
        WriteCommonKeys(mapping, clause);

        if (clause is IAnteScopedClause anteScoped && anteScoped.Antes.Length > 0)
            mapping.Set("antes", IntArrayNode(anteScoped.Antes), default);

        if (clause is IRollScopedClause rollScoped && !entry.RollsAreInlineValue)
        {
            var isDefault = entry.RollsDefault != null && rollScoped.Rolls.SequenceEqual(entry.RollsDefault);
            if (!isDefault)
                mapping.Set("rolls", IntArrayNode(rollScoped.Rolls), default);
        }

        var clauseKeys = JamlDiscriminatorRegistry.StaticStringArrayField(entry.ClauseType, "ClauseKeys");

        if (clauseKeys.Contains("with", StringComparer.OrdinalIgnoreCase))
        {
            var with = (JamlWith)entry.ClauseType.GetProperty("With")!.GetValue(clause)!;
            var withNode = WriteWith(with);
            if (withNode != null)
                mapping.Set("with", withNode, default);
        }

        if (clauseKeys.Contains("sources", StringComparer.OrdinalIgnoreCase) && entry.SourceConfigType is { } sourceType)
        {
            var sources = entry.ClauseType.GetProperty("Sources")!.GetValue(clause);
            var sourcesNode = WriteSourceConfig(sources, sourceType);
            if (sourcesNode != null)
                mapping.Set("sources", sourcesNode, default);
        }

        WriteExtraProperties(mapping, entry.ClauseType, clauseKeys, clause);

        return mapping;
    }

    private static void WriteCommonKeys(JMap mapping, IJamlClause clause)
    {
        if (clause.Label != null)
            mapping.Set("label", new JScalar(clause.Label), default);
        if (clause.Min != 1)
            mapping.Set("min", JScalar.Of(clause.Min), default);
        if (clause.Max.HasValue)
            mapping.Set("max", JScalar.Of(clause.Max.Value), default);
        // 1 is the unspecified-score default (see JamlConfigLoader.ParseClause) — writing it back
        // out would be indistinguishable from an author who explicitly typed "score: 1", which is
        // fine (both mean the same thing), but omitting it when it's the default keeps a
        // round-tripped file looking like what a human would actually write.
        if (clause.Score != 1)
            mapping.Set("score", JScalar.Of(clause.Score), default);
    }

    // erraticRank/erraticSuit read a scalar directly off the discriminator key (not an array);
    // standardCard/startingDraw have no discriminator value at all — their real content lives in
    // sibling keys the OverlayReader falls back to, so an empty block round-trips correctly.
    private static JNode DiscriminatorValueNode(string discriminator, JamlDiscriminatorEntry entry, IJamlClause clause)
    {
        if (entry.RollsAreInlineValue)
            return IntArrayNode(((IRollScopedClause)clause).Rolls);

        if (string.Equals(discriminator, "erraticRank", StringComparison.OrdinalIgnoreCase))
            return new JScalar(((ErraticRankClause)clause).Rank.ToString());
        if (string.Equals(discriminator, "erraticSuit", StringComparison.OrdinalIgnoreCase))
            return new JScalar(((ErraticSuitClause)clause).Suit.ToString());
        if (string.Equals(discriminator, "standardCard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(discriminator, "startingDraw", StringComparison.OrdinalIgnoreCase))
            return new JMap();

        return ItemArrayValueNode(discriminator, entry, clause);
    }

    private static JSeq ItemArrayValueNode(string discriminator, JamlDiscriminatorEntry entry, IJamlClause clause)
    {
        if (!ItemArrayPropertyByDiscriminator.TryGetValue(discriminator, out var propName))
            throw new InvalidOperationException($"ToJaml: no item-array mapping for discriminator '{discriminator}'.");

        var isWildcardProp = entry.ClauseType.GetProperty("IsWildcard");
        if (isWildcardProp != null && (bool)(isWildcardProp.GetValue(clause) ?? false))
            return StringArrayNode(["Any"]);

        var items = (System.Collections.IEnumerable)entry.ClauseType.GetProperty(propName)!.GetValue(clause)!;
        var names = new List<string>();
        foreach (var item in items)
            names.Add(item!.ToString()!);
        return StringArrayNode(names);
    }

    private static JMap? WriteWith(JamlWith with)
    {
        var mapping = new JMap();
        if (with.Luck != MotelyLuck.X1)
            mapping.Set("luck", new JScalar(with.Luck.ToString()), default);
        if (with.Vouchers.Length > 0)
            mapping.Set("vouchers", StringArrayNode(with.Vouchers.Select(v => v.ToString())), default);
        return mapping.Keys.Count == 0 ? null : mapping;
    }

    // Returns null ONLY when sources itself is null. A non-null-but-all-default config emits an
    // empty `sources: {}` mapping — the loader treats null (use DefaultSources) and explicit-empty
    // (override with "match nowhere") as distinct, so writing must preserve that distinction.
    private static JMap? WriteSourceConfig(object? sources, Type sourceType)
    {
        if (sources is null)
            return null;

        var sourceKeys = JamlDiscriminatorRegistry.StaticStringArrayField(sourceType, "SourceKeys");
        var mapping = new JMap();
        foreach (var group in sourceKeys.GroupBy(k => sourceType.GetProperty(ToPascalCase(ResolveWireKeyAlias(k)))))
        {
            if (group.Key is not { } prop)
                continue;
            var node = GenericPropertyToNode(prop, sources);
            if (node != null)
                mapping.Set(group.First(), node, default);
        }
        return mapping;
    }

    private static void WriteExtraProperties(JMap mapping, Type clauseType, IReadOnlyList<string> clauseKeys, object instance)
    {
        var extraKeys = clauseKeys.Where(k =>
            !PopulatorCommonKeys.Contains(k)
            && !string.Equals(k, "with", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(k, "sources", StringComparison.OrdinalIgnoreCase)
        );

        foreach (var group in extraKeys.GroupBy(k => clauseType.GetProperty(ToPascalCase(ResolveWireKeyAlias(k)))))
        {
            if (group.Key is not { } prop)
                continue;
            var node = GenericPropertyToNode(prop, instance);
            if (node != null)
                mapping.Set(group.First(), node, default);
        }
    }

    // The write-side mirror of SetGenericProperty — same type coverage, same default-value
    // suppression as the loader's own "?? default" fallbacks, so omitted-on-write round-trips to
    // omitted-on-read.
    private static JNode? GenericPropertyToNode(PropertyInfo prop, object instance)
    {
        var raw = prop.GetValue(instance);
        if (raw is null)
            return null;

        var type = prop.PropertyType;
        var underlying = Nullable.GetUnderlyingType(type);

        if (type == typeof(int))
            return (int)raw == 0 ? null : JScalar.Of((int)raw);
        if (type == typeof(bool))
            return (bool)raw ? JScalar.Of(true) : null;
        if (type == typeof(int[]))
            return ((int[])raw).Length == 0 ? null : IntArrayNode((int[])raw);
        if (type == typeof(string))
            return raw is string s && s.Length > 0 ? new JScalar(s) : null;
        if (underlying == typeof(MotelyStandardcardRank))
            return new JScalar(raw.ToString()!);
        if (underlying is { IsEnum: true })
            return new JScalar(raw.ToString()!);
        if (type.IsArray && type.GetElementType() is { IsEnum: true })
        {
            var names = new List<string>();
            foreach (var item in (System.Collections.IEnumerable)raw)
                names.Add(item!.ToString()!);
            return names.Count == 0 ? null : StringArrayNode(names);
        }

        throw new InvalidOperationException(
            $"ToJaml: unsupported property type '{type}' on {prop.DeclaringType?.Name}.{prop.Name}."
        );
    }

    private static JSeq IntArrayNode(IEnumerable<int> values)
    {
        var seq = new JSeq();
        foreach (var v in values)
            seq.Items.Add(JScalar.Of(v));
        return seq;
    }

    private static JSeq StringArrayNode(IEnumerable<string> values)
    {
        var seq = new JSeq();
        foreach (var v in values)
            seq.Items.Add(new JScalar(v));
        return seq;
    }

    // An integer never needs quoting — the writer already KNOWS it's an integer (JScalar.Of(int)
    // said so), so there's nothing to guess. Only genuinely free-text values (names, labels,
    // seeds) go through the disambiguation check below, and only because JAML's wire format is
    // text — a human-typed word can't carry its own "I am a string" tag the way a real int can.
    private static string ScalarText(JScalar scalar) =>
        scalar.Kind == JScalarKind.Integer ? scalar.Value : ScalarText(scalar.Value);

    // Quotes bare text only when it would otherwise misparse — colons, leading dashes, or values
    // that read as a different type (a numeric-looking name, "true"/"false"). Bare text is the
    // common case and stays bare, matching how the real corpus is authored.
    private static string ScalarText(string value)
    {
        bool needsQuote =
            value.Length == 0
            || value.Contains(':')
            || value.Contains('#')
            || value.StartsWith('-')
            || value.StartsWith('[')
            || value.Trim() != value;
        return needsQuote ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;
    }
}
