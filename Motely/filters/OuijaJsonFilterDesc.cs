using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely;

public struct OuijaJsonFilterDesc : IMotelySeedFilterDesc<OuijaJsonFilterDesc.OuijaJsonFilter>
{
    public OuijaConfig Config { get; }

    public OuijaJsonFilterDesc(OuijaConfig config) => Config = config;

    public OuijaJsonFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache tag streams for all antes in Needs/Wants
        foreach (var need in Config.Needs)
            ctx.CacheTagStream(need.DesireByAnte);
        foreach (var want in Config.Wants)
            ctx.CacheTagStream(want.DesireByAnte);
        return new OuijaJsonFilter(Config);
    }

    public struct OuijaJsonFilter : IMotelySeedFilter
    {
        public OuijaConfig Config { get; }
        public OuijaJsonFilter(OuijaConfig config) => Config = config;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            var jokerChoices = (MotelyJoker[])Enum.GetValues(typeof(MotelyJoker));
            VectorMask mask = VectorMask.AllBitsSet;
            // Collect all antes for tag needs
            var tagAntes = (Config.Needs ?? [])
                .Where(n => n.Type == "SmallBlindTag" || n.Type == "BigBlindTag")
                .SelectMany(n => n.SearchAntes)
                .Distinct()
                .OrderBy(a => a);
            // For each ante, create the tag stream ONCE, then advance for each tag need in config order
            foreach (var ante in tagAntes)
            {
                var tagStream = searchContext.CreateTagStream(ante);
                foreach (var need in (Config.Needs ?? []).Where(n => (n.Type == "SmallBlindTag" || n.Type == "BigBlindTag") && n.SearchAntes.Contains(ante)))
                {
                    var tag = searchContext.GetNextTag(ref tagStream);
                    if (need.Type == "BigBlindTag")
                        tag = searchContext.GetNextTag(ref tagStream);
                    mask &= VectorEnum256.Equals(tag, MotelyTagFromString(need.Value));
                    if (mask.IsAllFalse()) return mask;
                }
            }
            // Joker needs
            var jokerNeedsByAnte = (Config.Needs ?? [])
                .Where(n => n.Type != "SmallBlindTag" && n.Type != "BigBlindTag")
                .SelectMany(n => n.SearchAntes.Select(ante => (ante, n)))
                .GroupBy(x => x.ante, x => x.n)
                .ToDictionary(g => g.Key, g => g.ToList());
            // Process all antes in sorted order for determinism
            var allAntes = jokerNeedsByAnte.Keys.OrderBy(a => a);
            foreach (var ante in allAntes)
            {
                // Joker needs
                if (jokerNeedsByAnte.TryGetValue(ante, out var jokerNeeds))
                {
                    foreach (var need in jokerNeeds)
                    {
                        if (Enum.TryParse<MotelyJoker>(need.Value, out var joker))
                        {
                            var prng = searchContext.CreatePrngStream(MotelyPrngKeys.JokerSoul + ante);
                            var jokerVec = searchContext.GetNextRandomElement(ref prng, jokerChoices);
                            mask &= VectorEnum256.Equals(jokerVec, joker);
                            if (mask.IsAllFalse()) return mask;
                        }
                    }
                }
            }
            // Precompute wants grouped by ante for efficiency
            var wantsByAnte = (Config.Wants ?? [])
                .SelectMany(w => w.SearchAntes.Select(ante => (ante, w)))
                .GroupBy(x => x.ante, x => x.w)
                .ToDictionary(g => g.Key, g => g.ToArray());
            var antes = wantsByAnte.Keys.ToArray();
            return searchContext.SearchIndividualSeeds(mask, (ref MotelySingleSearchContext singleCtx) =>
            {
                int wantScore = 0;
                int negativeEditionCount = 0;
                foreach (var ante in antes)
                {
                    var prng = singleCtx.CreatePrngStream(MotelyPrngKeys.JokerSoul + ante);
                    var wants = wantsByAnte[ante];
                    foreach (var want in wants)
                    {
                        if (want.Type == "SmallBlindTag" || want.Type == "BigBlindTag")
                        {
                            var tagStream = singleCtx.CreateTagStream(ante);
                            var tag = singleCtx.GetNextTag(ref tagStream);
                            if (want.Type == "BigBlindTag")
                                tag = singleCtx.GetNextTag(ref tagStream);
                            if (tag.Equals(MotelyTagFromString(want.Value)))
                                wantScore++;
                        }
                        else if (Enum.TryParse<MotelyJoker>(want.Value, out var wantJoker))
                        {
                            var joker = singleCtx.GetNextRandomElement(ref prng, jokerChoices);
                            // TODO: Get edition if needed
                            if (joker.Equals(wantJoker))
                                wantScore++;
                        }
                        // Add more types as needed
                    }
                }
                // Reconstruct seed string from context (if possible)
                string seed = "(seed unavailable)";
                Console.WriteLine($"Seed: {seed}, WantScore: {wantScore}, NegativeEditions: {negativeEditionCount}");
                return true; // Accept all for now, add filtering logic as needed
            });
        }

        private static MotelyTag MotelyTagFromString(string value)
        {
            if (Enum.TryParse<MotelyTag>(value, out var tag))
                return tag;
            return default;
        }
    }

    public static OuijaJsonFilterDesc LoadFromFile(string configFileName)
    {
        var config = OuijaConfig.Load(configFileName, OuijaConfig.GetOptions());
        return new OuijaJsonFilterDesc(config);
    }
}
