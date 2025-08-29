using System.Runtime.Intrinsics;

namespace Motely;

public struct NaNSeedFilterDesc : IMotelySeedFilterDesc<NaNSeedFilterDesc.NaNSeedFilter>
{
    public string[] PseudoHashKeys { get; set; }
    
    public NaNSeedFilterDesc()
    {
        PseudoHashKeys = [
        // Joker probability/math operations
        // "lucky_money",
        // "lucky_mult", 
        // "misprint",
        // "bloodstone",
        // "parking",
        // "business",
        // "space",
        // "8ball",
        // "halu",
        // "gros_michel",
        // "cavendish",

        // // Boss blind effects
        // "wheel",
        // "hook",
        // "cerulean_bell",
        // "crimson_heart",

        // // Card modifications
        // "wheel_of_fortune",
        // "invisible",
        // "perkeo",
        // "madness",
        // "ankh_choice",

        // // Tarot/Spectral effects
        // "sigil",
        // "ouija",
        // "familiar_create",
        // "grim_create", 
        // "incantation_create",
        // //"random_destroy",
        // "spe_card",

        // // Pack/shop generation
        // "stdset",
        // "stdseal",
        // "stdsealtype",
        // "omen_globe",
        // "cert_fr",
        // "certsl",

        // // Other mechanics
        // "flipped_card",
        // "to_do",
        // "erratic",
        // "edition_deck"



        "lucky_money",
        "wheel"
        ];
    }

    public NaNSeedFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var key in PseudoHashKeys)
        {
            ctx.CachePseudoHash(key);
        }
        return new NaNSeedFilter(PseudoHashKeys);
    }

    public struct NaNSeedFilter(string[] pseudoHashKeys) : IMotelySeedFilter
    {
        public readonly string[] PseudoHashKeys = pseudoHashKeys;
            //magic numbers 0.3211483013596 and 0.6882134081454 found by MaximumG9
            Vector512<double> pointThreeTwo = Vector512.Create(0.3211483013596);
            Vector512<double> pointSixEight = Vector512.Create(0.6882134081454);

        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // Check each PRNG key for NaN values
            VectorMask resultMask = VectorMask.NoBitsSet;
            VectorMask resultMask2 = VectorMask.NoBitsSet;
            VectorMask resultMask3 = VectorMask.NoBitsSet;

            for (int i = 0; i < PseudoHashKeys.Length; i++)
            {
                var key = PseudoHashKeys[i];
                MotelyVectorPrngStream stream = searchContext.CreatePrngStream(key, true);
                Vector512<double> prng = searchContext.GetNextPrngState(ref stream);

                // Magic numbers match any seeds
                resultMask |= Vector512.Equals(prng, pointThreeTwo);
                resultMask2 |= Vector512.Equals(prng, pointSixEight);


                // Log if we found Magic Number Loop for this key
                if (resultMask.IsPartiallyTrue())
                {
                    Console.WriteLine($"\n🎰💥'{key}' produced a Magic Number Loop! (0.3211483013596)");
                }
                if (resultMask2.IsPartiallyTrue())
                {
                    Console.WriteLine($"\n🎰💥'{key}' produced a Magic Number Loop! (0.6882134081454)");
                }

                resultMask3 |= ~Vector512.Equals(prng, prng);
                if (resultMask3.IsPartiallyTrue())
                {
                    Console.WriteLine($"\n🎰💥'{key}' produced a NaN!");
                }
            }

            return resultMask | resultMask2 | resultMask3;
        }
    }
}
