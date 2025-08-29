using System.Runtime.Intrinsics;

namespace Motely;

public struct NaNSeedFilterDesc : IMotelySeedFilterDesc<NaNSeedFilterDesc.NaNSeedFilter>
{
    public string[] PseudoHashKeys { get; set; }
    
    public NaNSeedFilterDesc()
    {
        PseudoHashKeys = [
        //Joker probability/math operations
        "lucky_money",
        "lucky_mult", 
        "misprint",
        "bloodstone",
        "parking",
        "business",
        "space",
        "8ball",
        "halu",
        "gros_michel",
        "cavendish",

        // Boss blind effects
        "wheel",
        "hook",
        "cerulean_bell",
        "crimson_heart",

        // Card modifications
        "wheel_of_fortune",
        "invisible",
        "perkeo",
        "madness",
        "ankh_choice",

        // Tarot/Spectral effects
        "sigil",
        "ouija",
        "familiar_create",
        "grim_create", 
        "incantation_create",
        "random_destroy",
        "spe_card",

        // Pack/shop generation
        "stdset",
        "stdseal",
        "stdsealtype",
        "omen_globe",
        "cert_fr",
        "certsl",

        // Other mechanics
        "flipped_card",
        "to_do",
        "erratic",
        "edition_deck"
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

        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorMask resultMask = VectorMask.NoBitsSet;
            bool firstFind = true;

            for (int i = 0; i < PseudoHashKeys.Length; i++)
            {
                var key = PseudoHashKeys[i];
                MotelyVectorPrngStream stream = searchContext.CreatePrngStream(key, false);
                VectorMask resultMask3p2 = Vector512.Equals(stream.State, Vector512.Create(0.3211483013596));
                if (resultMask3p2.IsPartiallyTrue())
                {
                    if (firstFind)
                    {
                        firstFind = false;
                        Console.WriteLine("Found stream.State 0.3211483013596 value for key(s): ");
                        Console.Write(key);
                    }
                    else
                    {
                        Console.Write(", " + key);
                    }
                }
                resultMask |= resultMask3p2;
            }
            return VectorMask.AllBitsSet;
        }
    }
}
