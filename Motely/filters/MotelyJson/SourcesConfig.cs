using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Motely.Filters
{
    /// <summary>
    /// Configuration for where an item can be found in a Balatro seed.
    /// Supports shop slots, pack slots, tags, and special joker roll indices.
    /// </summary>
    public class SourcesConfig
    {
        public SourcesConfig()
        {
            // Initialize with null arrays - will be populated during ProcessClause if needed
            // Empty arrays in YAML deserialize to null, so we don't pre-initialize
        }

        [JsonPropertyName("shopSlots")]
        [YamlMember(Alias = "shopSlots")]
        public int[]? ShopSlots { get; set; }

        [JsonPropertyName("packSlots")]
        [YamlMember(Alias = "packSlots")]
        public int[]? PackSlots { get; set; }

        [JsonPropertyName("minShopSlot")]
        [YamlMember(Alias = "minShopSlot")]
        public int? MinShopSlot { get; set; }

        [JsonPropertyName("maxShopSlot")]
        [YamlMember(Alias = "maxShopSlot")]
        public int? MaxShopSlot { get; set; }

        [JsonPropertyName("minPackSlot")]
        [YamlMember(Alias = "minPackSlot")]
        public int? MinPackSlot { get; set; }

        [JsonPropertyName("maxPackSlot")]
        [YamlMember(Alias = "maxPackSlot")]
        public int? MaxPackSlot { get; set; }

        [JsonPropertyName("tags")]
        [YamlMember(Alias = "tags")]
        public bool? Tags { get; set; }

        [JsonPropertyName("requireMega")]
        [YamlMember(Alias = "requireMega")]
        public bool? RequireMega { get; set; }

        /// <summary>Judgement tarot joker roll indices (e.g. [0, 1] for first two uses)</summary>
        [JsonPropertyName("judgement")]
        [YamlMember(Alias = "judgement")]
        public int[]? Judgement { get; set; }

        /// <summary>Rare tag joker roll indices (e.g. [0] for first rare tag)</summary>
        [JsonPropertyName("rareTag")]
        [YamlMember(Alias = "rareTag")]
        public int[]? RareTag { get; set; }

        /// <summary>Uncommon tag joker roll indices (e.g. [0] for first uncommon tag)</summary>
        [JsonPropertyName("uncommonTag")]
        [YamlMember(Alias = "uncommonTag")]
        public int[]? UncommonTag { get; set; }

        /// <summary>RiffRaff joker roll indices (e.g. [0, 1] for first two RiffRaff jokers - RiffRaff creates 2 jokers)</summary>
        [JsonPropertyName("riffRaff")]
        [YamlMember(Alias = "riffRaff")]
        public int[]? RiffRaff { get; set; }

        /// <summary>Purple Seal / 8Ball tarot card roll indices (e.g. [0] for first tarot from Purple Seal or 8Ball - both use same PRNG key "8ba")</summary>
        [JsonPropertyName("purpleSealOrEightBall")]
        [YamlMember(Alias = "purpleSealOrEightBall")]
        public int[]? PurpleSealOrEightBall { get; set; }

        /// <summary>Emperor tarot card roll indices (e.g. [0, 1] for first two tarot cards from Emperor - Emperor creates 2 tarot cards)</summary>
        [JsonPropertyName("emperor")]
        [YamlMember(Alias = "emperor")]
        public int[]? Emperor { get; set; }

        /// <summary>SixthSense spectral card roll indices (e.g. [0] for first spectral card from SixthSense joker)</summary>
        [JsonPropertyName("sixthSense")]
        [YamlMember(Alias = "sixthSense")]
        public int[]? SixthSense { get; set; }

        /// <summary>Seance spectral card roll indices (e.g. [0, 1] for first two spectral cards from Seance joker)</summary>
        [JsonPropertyName("seance")]
        [YamlMember(Alias = "seance")]
        public int[]? Seance { get; set; }
    }
}
