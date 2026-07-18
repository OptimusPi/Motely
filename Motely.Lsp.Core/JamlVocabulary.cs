namespace Motely.Lsp.Core;

/// <summary>
/// The engine's enums as hover/completion vocabulary — the same anti-hallucination surface
/// <c>MotelyJaml.ListItems</c> serves over WASM, kept here so both shells read one list.
/// </summary>
public static class JamlVocabulary
{
    /// <summary>Every vocabulary enum paired with the human word hover uses for it.</summary>
    public static readonly IReadOnlyList<(Type EnumType, string Kind)> Enums =
    [
        (typeof(MotelyJoker), "joker"),
        (typeof(MotelyVoucher), "voucher"),
        (typeof(MotelyTag), "tag"),
        (typeof(MotelyBossBlind), "boss"),
        (typeof(MotelyDeck), "deck"),
        (typeof(MotelyStake), "stake"),
        (typeof(MotelyItemEdition), "edition"),
        (typeof(MotelyItemSeal), "seal"),
        (typeof(MotelyItemEnhancement), "enhancement"),
        (typeof(MotelyTarotCard), "tarot card"),
        (typeof(MotelySpectralCard), "spectral card"),
        (typeof(MotelyPlanetCard), "planet card"),
    ];

    /// <summary>The enum a clause or root key takes its value from, when it has one.</summary>
    public static Type? EnumForKey(string key) => key.ToLowerInvariant() switch
    {
        "deck" => typeof(MotelyDeck),
        "stake" => typeof(MotelyStake),
        "edition" => typeof(MotelyItemEdition),
        "seal" => typeof(MotelyItemSeal),
        "enhancement" => typeof(MotelyItemEnhancement),
        "rank" => typeof(MotelyStandardcardRank),
        "suit" => typeof(MotelyStandardcardSuit),
        "stickers" => typeof(MotelyJokerSticker),
        "vouchers" => typeof(MotelyVoucher),
        _ => null,
    };
}
