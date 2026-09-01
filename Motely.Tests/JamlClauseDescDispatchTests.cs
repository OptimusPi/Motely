using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// <see cref="JamlClauseDescDispatch"/> is one switch over every wire family. The failure it
/// invites is silent: add a FilterDesc, forget the dispatch arm, and the clause loads as an empty
/// shell that matches everything instead of erroring.
///
/// So the cases here are driven from <c>JamlSchema</c> — the generated index of the descs — rather
/// than from a hand-kept list. A new family joins the schema and these tests demand its arm on the
/// same commit; there is no second place to update.
/// </summary>
public sealed class JamlClauseDescDispatchTests
{
    public static TheoryData<string> AllDiscriminators()
    {
        var data = new TheoryData<string>();
        foreach (var disc in JamlSchema.Discriminators)
            data.Add(disc);
        return data;
    }

    /// <summary>
    /// Every wire family except the <c>and</c>/<c>or</c> groupings, which are schema entries but
    /// not desc families — the loader handles their nesting itself.
    /// </summary>
    public static TheoryData<string> DescFamilyDiscriminators()
    {
        var data = new TheoryData<string>();
        foreach (var disc in JamlSchema.Discriminators)
        {
            if (JamlSchema.CreateClause(disc) is AndClause or OrClause)
                continue;
            data.Add(disc);
        }
        return data;
    }

    /// <summary>
    /// A key's type is the desc's business, not this test's. Offering one well-formed value of
    /// each shape and accepting the first one claimed keeps the test off the descs' turf while
    /// still proving the key is wired.
    /// </summary>
    private static IEnumerable<JamlLoaderValueReader> CandidateValuesFor(string key)
    {
        if (JamlSchema.KeyValueEnumTypeFor(key) is { IsEnum: true } enumType)
            yield return JamlLoaderValueReader.FromScalar(Enum.GetNames(enumType)[0]);

        yield return JamlLoaderValueReader.FromScalar("1");
        yield return JamlLoaderValueReader.FromScalar("true");
        yield return JamlLoaderValueReader.FromStrings(["0", "1"]);
        yield return JamlLoaderValueReader.FromScalar("any");
    }

    private static bool AnyCandidateClaims(string disc, string key) =>
        CandidateValuesFor(key)
            .Any(value => JamlClauseDescDispatch.TrySet(JamlSchema.CreateClause(disc), key, value));

    private static JamlLoaderValueReader DiscriminatorValueFor(string disc)
    {
        if (JamlSchema.ValueEnumTypeFor(disc) is { IsEnum: true } enumType)
            return JamlLoaderValueReader.FromScalar(Enum.GetNames(enumType)[0]);
        if (JamlSchema.RollsAreInlineFor(disc))
            return JamlLoaderValueReader.FromStrings(["0"]);
        return JamlLoaderValueReader.FromScalar("any");
    }

    [Fact]
    public void SchemaExposesEveryWireFamily() =>
        Assert.True(
            JamlSchema.Discriminators.Length >= 20,
            $"expected the generated schema to carry the wire families, saw {JamlSchema.Discriminators.Length}"
        );

    [Theory]
    [MemberData(nameof(DescFamilyDiscriminators))]
    public void EveryDiscriminator_HasADispatchArm(string disc)
    {
        var clause = JamlSchema.CreateClause(disc);

        bool handled = JamlClauseDescDispatch.TrySetDiscriminatorValue(
            clause,
            DiscriminatorValueFor(disc)
        );

        Assert.True(
            handled,
            $"'{disc}' produced {clause.GetType().Name} but no arm in "
                + $"{nameof(JamlClauseDescDispatch)}.{nameof(JamlClauseDescDispatch.TrySetDiscriminatorValue)} claimed it"
        );
    }

    /// <summary>
    /// Keys the loader owns for every family. A desc's <c>Set</c> correctly declines these, which
    /// is why the assertion below is scoped to the keys a family actually adds on top of them.
    /// </summary>
    private static readonly HashSet<string> CommonKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "min",
        "max",
        "score",
        "label",
        "ante",
        "antes",
        "sources",
        "rolls",
        "with",
    };

    /// <summary>
    /// Every key a family advertises beyond the common ones must actually be wired into its
    /// <c>Set</c>. An advertised-but-unwired key is the worst kind: the editor offers it, the
    /// loader accepts the document, and the value is silently dropped.
    /// </summary>
    [Theory]
    [MemberData(nameof(DescFamilyDiscriminators))]
    public void EveryDiscriminator_ClaimsTheKeysItAddsBeyondTheCommonOnes(string disc)
    {
        var keys = JamlSchema.ClauseKeysFor(disc);
        Assert.NotEmpty(keys);

        var ownKeys = keys.Where(static k => !CommonKeys.Contains(k)).ToArray();
        if (ownKeys.Length == 0)
            return; // family adds nothing beyond the loader-owned keys

        var unclaimed = ownKeys.Where(key => !AnyCandidateClaims(disc, key)).ToArray();

        Assert.True(
            unclaimed.Length == 0,
            $"'{disc}' advertises [{string.Join(", ", ownKeys)}] but its desc never claimed "
                + $"[{string.Join(", ", unclaimed)}] for any well-formed value"
        );
    }

    [Theory]
    [MemberData(nameof(DescFamilyDiscriminators))]
    public void EveryDiscriminator_DeclinesTheLoaderOwnedKeys(string disc)
    {
        foreach (var key in JamlSchema.ClauseKeysFor(disc).Where(CommonKeys.Contains))
        {
            Assert.False(
                AnyCandidateClaims(disc, key),
                $"'{disc}' desc claimed loader-owned key '{key}' — two owners for one key"
            );
        }
    }

    [Fact]
    public void UnknownKey_IsDeclinedNotSwallowed()
    {
        var clause = JamlSchema.CreateClause("voucher");

        Assert.False(
            JamlClauseDescDispatch.TrySet(
                clause,
                "definitelyNotAKey",
                JamlLoaderValueReader.FromScalar("1")
            )
        );
    }

    /// <summary>
    /// Logic groupings are not desc families. Both entry points must decline them so the loader
    /// falls back to its and/or handling instead of treating them as a wire clause.
    /// </summary>
    [Fact]
    public void LogicGroupings_AreNotDescFamilies()
    {
        foreach (IJamlClause logic in new IJamlClause[] { new AndClause(), new OrClause() })
        {
            Assert.False(
                JamlClauseDescDispatch.TrySetDiscriminatorValue(
                    logic,
                    JamlLoaderValueReader.FromScalar("any")
                )
            );
            Assert.False(
                JamlClauseDescDispatch.TrySet(
                    logic,
                    "antes",
                    JamlLoaderValueReader.FromStrings(["1"])
                )
            );
        }
    }

    [Fact]
    public void CreateClause_RejectsAnUnknownWire() =>
        Assert.Throws<InvalidOperationException>(
            () => JamlSchema.CreateClause("notAWireFamilyAtAll")
        );
}
