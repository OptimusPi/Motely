using System.Reflection;
using SharpYaml.Model;

namespace Motely.Filters.Jaml;

// The write-side mirror of JamlClausePopulator: turns a JamlConfig back into JAML text using the
// exact same ClauseKeys/SourceKeys reflection the loader reads, so a round trip through
// FromYaml(ToYaml(config)) reproduces the same clause data. Not guaranteed to reproduce the
// original TEXT (e.g. "smallBlindTag"/"bigBlindTag" both come back out as "tag" with an explicit
// rolls: block; erraticRanks shorthand comes back out as "or" — both are real, parseable,
// semantically-identical JAML) — this is a data round trip, not a byte-for-byte one.
public static partial class JamlConfigLoader
{
    // Every discriminator ToYaml is willing to emit for a given clause CLR type. Order matters:
    // for types with more than one valid spelling (TagClause backs tag/smallBlindTag/bigBlindTag;
    // JokerClause backs joker/jokers), the first entry here wins and becomes canonical.
    private static readonly string[] CanonicalDiscriminatorOrder =
    [
        "joker", "commonJoker", "uncommonJoker", "rareJoker", "legendaryJoker",
        "voucher", "tarotCard", "spectralCard", "planetCard", "standardCard",
        "boss", "tag", "smallBlindTag", "bigBlindTag",
        "erraticRank", "erraticSuit", "startingDraw",
        "luckyMoney", "luckyMult", "misprintMult", "wheelOfFortune",
        "grosMichelExtinct", "cavendishExtinct", "spaceLevelup", "businessPayout",
        "bloodstoneTrigger", "parkingPayout", "glassDestroy", "wheelStaysFlipped",
    ];

    private static readonly Dictionary<Type, string> CanonicalDiscriminatorByClauseType = BuildCanonicalDiscriminatorMap();

    private static Dictionary<Type, string> BuildCanonicalDiscriminatorMap()
    {
        var map = new Dictionary<Type, string>();
        foreach (var discriminator in CanonicalDiscriminatorOrder)
        {
            var clauseType = JamlDiscriminatorRegistry.Entries[discriminator].ClauseType;
            if (!map.ContainsKey(clauseType))
                map[clauseType] = discriminator;
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

    public static string ToYaml(JamlConfig config)
    {
        var root = new YamlMapping { { new YamlValue("id"), new YamlValue(config.Id) } };
        if (config.Name != null)
            root.Add(new YamlValue("name"), new YamlValue(config.Name));
        if (config.Description != null)
            root.Add(new YamlValue("description"), new YamlValue(config.Description));
        if (config.Author != null)
            root.Add(new YamlValue("author"), new YamlValue(config.Author));
        if (config.Deck != MotelyDeck.Red)
            root.Add(new YamlValue("deck"), new YamlValue(config.Deck.ToString()));
        if (config.Stake != MotelyStake.White)
            root.Add(new YamlValue("stake"), new YamlValue(config.Stake.ToString()));
        if (config.Seeds.Count > 0)
            root.Add(new YamlValue("seeds"), StringArrayNode(config.Seeds));
        if (config.Must.Count > 0)
            root.Add(new YamlValue("must"), ClauseListNode(config.Must));
        if (config.Should.Count > 0)
            root.Add(new YamlValue("should"), ClauseListNode(config.Should));
        if (config.MustNot.Count > 0)
            root.Add(new YamlValue("mustNot"), ClauseListNode(config.MustNot));

        var doc = new YamlDocument { Contents = root };
        return doc.ToString();
    }

    private static YamlSequence ClauseListNode(IEnumerable<IJamlClause> clauses)
    {
        var seq = new YamlSequence();
        foreach (var clause in clauses)
            seq.Add(WriteClause(clause));
        return seq;
    }

    private static YamlMapping WriteClause(IJamlClause clause)
    {
        if (clause is LogicClause logic)
        {
            var logicDiscriminator = logic is AndClause ? "and" : "or";
            var logicMapping = new YamlMapping
            {
                { new YamlValue(logicDiscriminator), ClauseListNode(logic.Clauses) },
            };
            WriteCommonKeys(logicMapping, logic);
            return logicMapping;
        }

        var type = clause.GetType();
        if (!CanonicalDiscriminatorByClauseType.TryGetValue(type, out var discriminator))
            throw new InvalidOperationException($"ToYaml: no discriminator registered for clause type '{type.Name}'.");
        var entry = JamlDiscriminatorRegistry.Entries[discriminator];

        var mapping = new YamlMapping
        {
            { new YamlValue(discriminator), DiscriminatorValueNode(discriminator, entry, clause) },
        };
        WriteCommonKeys(mapping, clause);

        if (clause is IAnteScopedClause anteScoped && anteScoped.Antes.Length > 0)
            mapping.Add(new YamlValue("antes"), IntArrayNode(anteScoped.Antes));

        if (clause is IRollScopedClause rollScoped && !entry.RollsAreInlineValue)
        {
            var isDefault = entry.RollsDefault != null && rollScoped.Rolls.SequenceEqual(entry.RollsDefault);
            if (!isDefault)
                mapping.Add(new YamlValue("rolls"), IntArrayNode(rollScoped.Rolls));
        }

        var clauseKeys = JamlDiscriminatorRegistry.StaticStringArrayField(entry.ClauseType, "ClauseKeys");

        if (clauseKeys.Contains("with", StringComparer.OrdinalIgnoreCase))
        {
            var with = (JamlWith)entry.ClauseType.GetProperty("With")!.GetValue(clause)!;
            var withNode = WriteWith(with);
            if (withNode != null)
                mapping.Add(new YamlValue("with"), withNode);
        }

        if (clauseKeys.Contains("sources", StringComparer.OrdinalIgnoreCase) && entry.SourceConfigType is { } sourceType)
        {
            var sources = entry.ClauseType.GetProperty("Sources")!.GetValue(clause);
            var sourcesNode = WriteSourceConfig(sources, sourceType);
            if (sourcesNode != null)
                mapping.Add(new YamlValue("sources"), sourcesNode);
        }

        WriteExtraProperties(mapping, entry.ClauseType, clauseKeys, clause);

        return mapping;
    }

    private static void WriteCommonKeys(YamlMapping mapping, IJamlClause clause)
    {
        if (clause.Label != null)
            mapping.Add(new YamlValue("label"), new YamlValue(clause.Label));
        if (clause.Min != 1)
            mapping.Add(new YamlValue("min"), new YamlValue(clause.Min));
        if (clause.Max.HasValue)
            mapping.Add(new YamlValue("max"), new YamlValue(clause.Max.Value));
        if (clause.Score != 0)
            mapping.Add(new YamlValue("score"), new YamlValue(clause.Score));
    }

    // erraticRank/erraticSuit read a scalar directly off the discriminator key (not an array);
    // standardCard/startingDraw have no discriminator value at all — their real content lives in
    // sibling keys the OverlayReader falls back to, so an empty block round-trips correctly.
    private static YamlElement DiscriminatorValueNode(string discriminator, JamlDiscriminatorEntry entry, IJamlClause clause)
    {
        if (entry.RollsAreInlineValue)
            return IntArrayNode(((IRollScopedClause)clause).Rolls);

        if (string.Equals(discriminator, "erraticRank", StringComparison.OrdinalIgnoreCase))
            return new YamlValue(((ErraticRankClause)clause).Rank.ToString());
        if (string.Equals(discriminator, "erraticSuit", StringComparison.OrdinalIgnoreCase))
            return new YamlValue(((ErraticSuitClause)clause).Suit.ToString());
        if (string.Equals(discriminator, "standardCard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(discriminator, "startingDraw", StringComparison.OrdinalIgnoreCase))
            return new YamlMapping();

        return ItemArrayValueNode(discriminator, entry, clause);
    }

    private static YamlSequence ItemArrayValueNode(string discriminator, JamlDiscriminatorEntry entry, IJamlClause clause)
    {
        if (!ItemArrayPropertyByDiscriminator.TryGetValue(discriminator, out var propName))
            throw new InvalidOperationException($"ToYaml: no item-array mapping for discriminator '{discriminator}'.");

        var isWildcardProp = entry.ClauseType.GetProperty("IsWildcard");
        if (isWildcardProp != null && (bool)(isWildcardProp.GetValue(clause) ?? false))
            return StringArrayNode(["Any"]);

        var items = (System.Collections.IEnumerable)entry.ClauseType.GetProperty(propName)!.GetValue(clause)!;
        var names = new List<string>();
        foreach (var item in items)
            names.Add(item!.ToString()!);
        return StringArrayNode(names);
    }

    private static YamlMapping? WriteWith(JamlWith with)
    {
        var mapping = new YamlMapping();
        if (with.Luck != MotelyLuck.X1)
            mapping.Add(new YamlValue("luck"), new YamlValue(with.Luck.ToString()));
        if (with.Vouchers.Length > 0)
            mapping.Add(new YamlValue("vouchers"), StringArrayNode(with.Vouchers.Select(v => v.ToString())));
        return mapping.Count == 0 ? null : mapping;
    }

    // Returns null ONLY when sources itself is null. A non-null-but-all-default config emits an
    // empty `sources: {}` mapping — the loader treats null (use DefaultSources) and explicit-empty
    // (override with "match nowhere") as distinct, so writing must preserve that distinction.
    private static YamlMapping? WriteSourceConfig(object? sources, Type sourceType)
    {
        if (sources is null)
            return null;

        var sourceKeys = JamlDiscriminatorRegistry.StaticStringArrayField(sourceType, "SourceKeys");
        var mapping = new YamlMapping();
        foreach (var group in sourceKeys.GroupBy(k => sourceType.GetProperty(ToPascalCase(ResolveWireKeyAlias(k)))))
        {
            if (group.Key is not { } prop)
                continue;
            var node = GenericPropertyToNode(prop, sources);
            if (node != null)
                mapping.Add(new YamlValue(group.First()), node);
        }
        return mapping;
    }

    private static void WriteExtraProperties(YamlMapping mapping, Type clauseType, IReadOnlyList<string> clauseKeys, object instance)
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
                mapping.Add(new YamlValue(group.First()), node);
        }
    }

    // The write-side mirror of SetGenericProperty — same type coverage, same default-value
    // suppression as the loader's own "?? default" fallbacks, so omitted-on-write round-trips to
    // omitted-on-read.
    private static YamlElement? GenericPropertyToNode(PropertyInfo prop, object instance)
    {
        var raw = prop.GetValue(instance);
        if (raw is null)
            return null;

        var type = prop.PropertyType;
        var underlying = Nullable.GetUnderlyingType(type);

        if (type == typeof(int))
            return (int)raw == 0 ? null : new YamlValue((int)raw);
        if (type == typeof(bool))
            return (bool)raw ? new YamlValue(true) : null;
        if (type == typeof(int[]))
            return ((int[])raw).Length == 0 ? null : IntArrayNode((int[])raw);
        if (type == typeof(string))
            return raw is string s && s.Length > 0 ? new YamlValue(s) : null;
        if (underlying == typeof(MotelyStandardcardRank))
            return new YamlValue(raw.ToString()!);
        if (underlying is { IsEnum: true })
            return new YamlValue(raw.ToString()!);
        if (type.IsArray && type.GetElementType() is { IsEnum: true })
        {
            var names = new List<string>();
            foreach (var item in (System.Collections.IEnumerable)raw)
                names.Add(item!.ToString()!);
            return names.Count == 0 ? null : StringArrayNode(names);
        }

        throw new InvalidOperationException(
            $"ToYaml: unsupported property type '{type}' on {prop.DeclaringType?.Name}.{prop.Name}."
        );
    }

    private static YamlSequence IntArrayNode(IEnumerable<int> values)
    {
        var seq = new YamlSequence();
        foreach (var v in values)
            seq.Add(new YamlValue(v));
        return seq;
    }

    private static YamlSequence StringArrayNode(IEnumerable<string> values)
    {
        var seq = new YamlSequence();
        foreach (var v in values)
            seq.Add(new YamlValue(v));
        return seq;
    }
}
