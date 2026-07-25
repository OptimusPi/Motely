namespace Motely.Filters.Jaml;

// Concrete clause construction + desc Set/SetDiscriminatorValue. No Activator, no GetProperty —
// the compiler sees every type the loader builds (NativeAOT-friendly). Source blocks are typed
// parsers next to each SourceConfig shape.
public static partial class JamlConfigLoader
{
    // Keys every populated clause handles itself before the desc Set loop runs.
    private static readonly HashSet<string> PopulatorCommonKeys =
        new(StringComparer.OrdinalIgnoreCase) { "min", "max", "score", "label", "ante", "antes", "rolls" };

    private static IJamlClause Populate(
        string discriminator,
        NodeReader node,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
    {
        var clause = CreateClause(discriminator);
        clause.Label = label;
        clause.Min = min;
        clause.Max = max;
        clause.Score = score;

        if (clause is IAnteScopedClause anteScoped)
            anteScoped.Antes = antes;

        if (clause is IRollScopedClause rollScoped)
        {
            rollScoped.Rolls = JamlSchema.RollsAreInlineFor(discriminator)
                ? node.GetIntArray(discriminator) ?? []
                : data.GetIntArray("rolls") ?? JamlSchema.RollsDefaultFor(discriminator) ?? [];
        }

        ApplyWith(clause, data);
        ApplySources(clause, data);

        var clauseKeys = JamlSchema.ClauseKeysFor(discriminator);
        var extraKeys = clauseKeys.Where(k =>
            !PopulatorCommonKeys.Contains(k)
            && !string.Equals(k, "with", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(k, "sources", StringComparison.OrdinalIgnoreCase)
        );

        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in extraKeys)
        {
            if (applied.Contains(key))
                continue;
            if (!KeyPresent(data, key) && !KeyPresent(data, ResolveWireKeyAlias(key)))
                continue;

            var reader = ValueReaderForKey(data, key);
            if (!JamlClauseDescDispatch.TrySet(clause, key, reader))
            {
                throw new InvalidOperationException(
                    $"Populator: '{clause.GetType().Name}' has no Set arm for key '{key}'."
                );
            }

            applied.Add(key);
            if (string.Equals(key, "mult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "value", StringComparison.OrdinalIgnoreCase))
            {
                applied.Add("mult");
                applied.Add("value");
            }
            if (string.Equals(key, "requireMega", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "requireMegaPack", StringComparison.OrdinalIgnoreCase))
            {
                applied.Add("requireMega");
                applied.Add("requireMegaPack");
            }
        }

        return clause;
    }

    private static IJamlClause CreateClause(string discriminator) =>
        Normalize(discriminator) switch
        {
            "joker" or "jokers" => new JokerClause(),
            "commonjoker" or "commonjokers" => new CommonJokerClause(),
            "uncommonjoker" or "uncommonjokers" => new UncommonJokerClause(),
            "rarejoker" or "rarejokers" => new RareJokerClause(),
            "legendaryjoker" or "legendaryjokers" => new LegendaryJokerClause(),
            "voucher" or "vouchers" => new VoucherClause { Vouchers = [], Rolls = [] },
            "tarotcard" or "tarotcards" => new TarotCardClause { Tarots = [] },
            "spectralcard" or "spectralcards" => new SpectralCardClause { Spectrals = [] },
            "planetcard" or "planetcards" => new PlanetCardClause { Planets = [] },
            "standardcard" or "standardcards" => new StandardCardClause(),
            "boss" or "bosses" => new BossClause { Bosses = [] },
            "tag" or "tags" or "smallblindtag" or "bigblindtag" => new TagClause { Tags = [], Rolls = [] },
            "erraticrank" or "erraticranks" => new ErraticRankClause { Rank = default },
            "erraticsuit" or "erraticsuits" => new ErraticSuitClause { Suit = default },
            "startingdraw" => new StartingDrawClause(),
            "luckymoney" => new LuckyMoneyClause(),
            "luckymult" => new LuckyMultClause(),
            "misprintmult" => new MisprintMultClause(),
            "wheeloffortune" => new WheelOfFortuneClause(),
            "grosmichelextinct" => new GrosMichelExtinctClause(),
            "cavendishextinct" => new CavendishExtinctClause(),
            "spacelevelup" => new SpaceLevelupClause(),
            "businesspayout" => new BusinessPayoutClause(),
            "bloodstonetrigger" => new BloodstoneTriggerClause(),
            "parkingpayout" => new ParkingPayoutClause(),
            "glassdestroy" => new GlassDestroyClause(),
            "wheelstaysflipped" => new WheelStaysFlippedClause(),
            _ => throw new InvalidOperationException(
                $"Populator: cannot construct clause for discriminator '{discriminator}'."
            ),
        };

    private static void ApplyWith(IJamlClause clause, IReader data)
    {
        // Only families that list "with" in ClauseKeys own a With property.
        switch (clause)
        {
            case LuckyMoneyClause c:
                c.With = ParseWith(data);
                break;
            case LuckyMultClause c:
                c.With = ParseWith(data);
                break;
            case WheelOfFortuneClause c:
                c.With = ParseWith(data);
                break;
            case GrosMichelExtinctClause c:
                c.With = ParseWith(data);
                break;
            case CavendishExtinctClause c:
                c.With = ParseWith(data);
                break;
            case SpaceLevelupClause c:
                c.With = ParseWith(data);
                break;
            case GlassDestroyClause c:
                c.With = ParseWith(data);
                break;
            case WheelStaysFlippedClause c:
                c.With = ParseWith(data);
                break;
        }
    }

    private static void ApplySources(IJamlClause clause, IReader data)
    {
        switch (clause)
        {
            case JokerClause c:
                c.Sources = PopulateJokerSources(data);
                break;
            case CommonJokerClause c:
                c.Sources = PopulateJokerSources(data);
                break;
            case UncommonJokerClause c:
                c.Sources = PopulateJokerSources(data);
                break;
            case RareJokerClause c:
                c.Sources = PopulateJokerSources(data);
                break;
            case LegendaryJokerClause c:
                c.Sources = PopulateLegendaryJokerSources(data);
                break;
            case TarotCardClause c:
                c.Sources = PopulateTarotSources(data);
                break;
            case SpectralCardClause c:
                c.Sources = PopulateSpectralSources(data);
                break;
            case PlanetCardClause c:
                c.Sources = PopulatePlanetSources(data);
                break;
            case StandardCardClause c:
                c.Sources = PopulateStandardSources(data);
                break;
        }
    }

    private static bool KeyPresent(IReader data, string key) =>
        data.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    private static JamlLoaderValueReader ValueReaderForKey(IReader data, string key)
    {
        if (data.GetStringArray(key) is { } strings)
            return JamlLoaderValueReader.FromStrings(strings);
        if (data.GetIntArray(key) is { } ints)
            return JamlLoaderValueReader.FromStrings(Array.ConvertAll(ints, i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (data.GetInt(key) is { } i)
            return JamlLoaderValueReader.FromScalar(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (data.GetBool(key) is { } b)
            return JamlLoaderValueReader.FromScalar(b ? "true" : "false");
        return JamlLoaderValueReader.FromScalar(data.GetString(key));
    }

    private static JokerSourceConfig? PopulateJokerSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block is null)
            return null;
        ValidateKeys(block, JokerSourceConfig.SourceKeys, $"{nameof(JokerSourceConfig)} source");
        return new JokerSourceConfig
        {
            ShopItems = block.GetIntArray("shopItems") ?? [],
            BoosterPacks = block.GetIntArray("boosterPacks") ?? [],
            Judgement = block.GetIntArray("judgement") ?? [],
            Wraith = block.GetIntArray("wraith") ?? [],
            RiffRaff = block.GetIntArray("riffRaff") ?? [],
            RareTag = block.GetIntArray("rareTag") ?? [],
            UncommonTag = block.GetIntArray("uncommonTag") ?? [],
            CommonShopJokers = block.GetIntArray("commonShopJokers") ?? [],
            UncommonShopJokers = block.GetIntArray("uncommonShopJokers") ?? [],
            RareShopJokers = block.GetIntArray("rareShopJokers") ?? [],
            AllShopJokers = block.GetIntArray("allShopJokers") ?? [],
        };
    }

    private static LegendaryJokerSourceConfig? PopulateLegendaryJokerSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block is null)
            return null;
        ValidateKeys(block, LegendaryJokerSourceConfig.SourceKeys, $"{nameof(LegendaryJokerSourceConfig)} source");
        return new LegendaryJokerSourceConfig
        {
            BoosterPacks = block.GetIntArray("boosterPacks") ?? [],
            ArcanaPacks = block.GetIntArray("arcanaPacks") ?? [],
            SpectralPacks = block.GetIntArray("spectralPacks") ?? [],
            SoulCard = block.GetIntArray("soulCard") ?? [],
            RequireMegaPack =
                block.GetBool("requireMegaPack")
                ?? block.GetBool("requireMega")
                ?? false,
        };
    }

    private static TarotCardSourceConfig? PopulateTarotSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block is null)
            return null;
        ValidateKeys(block, TarotCardSourceConfig.SourceKeys, $"{nameof(TarotCardSourceConfig)} source");
        return new TarotCardSourceConfig
        {
            ShopItems = block.GetIntArray("shopItems") ?? [],
            BoosterPacks = block.GetIntArray("boosterPacks") ?? [],
            Emperor = block.GetIntArray("emperor") ?? [],
            PurpleSealOrEightBall = block.GetIntArray("purpleSealOrEightBall") ?? [],
            CharmTag = block.GetBool("charmTag") ?? false,
        };
    }

    private static SpectralCardSourceConfig? PopulateSpectralSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block is null)
            return null;
        ValidateKeys(block, SpectralCardSourceConfig.SourceKeys, $"{nameof(SpectralCardSourceConfig)} source");
        return new SpectralCardSourceConfig
        {
            ShopItems = block.GetIntArray("shopItems") ?? [],
            BoosterPacks = block.GetIntArray("boosterPacks") ?? [],
            SixthSense = block.GetIntArray("sixthSense") ?? [],
            Seance = block.GetIntArray("seance") ?? [],
            EtherealTag = block.GetBool("etherealTag") ?? false,
            RequireMegaPack =
                block.GetBool("requireMegaPack")
                ?? block.GetBool("requireMega")
                ?? false,
        };
    }

    private static PlanetSourceConfig? PopulatePlanetSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block is null)
            return null;
        ValidateKeys(block, PlanetSourceConfig.SourceKeys, $"{nameof(PlanetSourceConfig)} source");
        return new PlanetSourceConfig
        {
            ShopItems = block.GetIntArray("shopItems") ?? [],
            BoosterPacks = block.GetIntArray("boosterPacks") ?? [],
        };
    }

    private static StandardCardSourceConfig? PopulateStandardSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block is null)
            return null;
        ValidateKeys(block, StandardCardSourceConfig.SourceKeys, $"{nameof(StandardCardSourceConfig)} source");
        return new StandardCardSourceConfig
        {
            ShopItems = block.GetIntArray("shopItems") ?? [],
            BoosterPacks = block.GetIntArray("boosterPacks") ?? [],
            Certificate = block.GetIntArray("certificate") ?? [],
            Incantation = block.GetIntArray("incantation") ?? [],
            Familiar = block.GetIntArray("familiar") ?? [],
            Grim = block.GetIntArray("grim") ?? [],
            DeckDraw = block.GetIntArray("deckDraw") ?? [],
        };
    }

    // requireMega/requireMegaPack and mult/value — deliberate aliases, not a reflection convention.
    private static string ResolveWireKeyAlias(string key) =>
        string.Equals(key, "requireMega", StringComparison.OrdinalIgnoreCase) ? "requireMegaPack"
        : string.Equals(key, "value", StringComparison.OrdinalIgnoreCase) ? "mult"
        : key;

    private static IJamlClause PopulateJokerFamily<TEnum>(
        string discriminator,
        NodeReader node,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
        where TEnum : struct, Enum
    {
        var clause = Populate(discriminator, node, data, antes, min, max, score, label);
        var discReader = DiscriminatorValueReader(node, discriminator);
        if (!JamlClauseDescDispatch.TrySetDiscriminatorValue(clause, discReader))
            throw new InvalidOperationException($"'{discriminator}' clause requires a value.");
        return clause;
    }

    private static TClause PopulateAndCast<TClause>(
        string discriminator,
        NodeReader node,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label,
        bool applyDiscriminatorValue = true
    )
        where TClause : class, IJamlClause
    {
        var clause = (TClause)Populate(discriminator, node, data, antes, min, max, score, label);
        if (applyDiscriminatorValue)
        {
            var discReader = DiscriminatorValueReader(node, discriminator);
            if (!JamlClauseDescDispatch.TrySetDiscriminatorValue(clause, discReader))
                throw new InvalidOperationException($"'{discriminator}' clause requires a value.");
        }
        return clause;
    }

    private static JamlLoaderValueReader DiscriminatorValueReader(NodeReader node, string discriminator)
    {
        if (node.GetStringArray(discriminator) is { } arr)
            return JamlLoaderValueReader.FromStrings(arr);
        if (node.GetIntArray(discriminator) is { } ints)
            return JamlLoaderValueReader.FromStrings(
                Array.ConvertAll(ints, i => i.ToString(System.Globalization.CultureInfo.InvariantCulture))
            );
        return JamlLoaderValueReader.FromScalar(node.GetString(discriminator));
    }
}
