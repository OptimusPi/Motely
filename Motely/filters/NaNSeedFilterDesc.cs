using System.Runtime.Intrinsics;

namespace Motely;

public struct NaNSeedFilterDesc : IMotelySeedFilterDesc<NaNSeedFilterDesc.NaNSeedFilter>
{
    public string[] PseudoHashKeys { get; set; }
    
    public NaNSeedFilterDesc()
    {
        var keys = new List<string>();
        
        // Simple keys (no concatenation needed)
        keys.AddRange([
            // Joker probability/RNG operations (VERIFIED FROM LUA)
            "lucky_money",      // Lucky Card money chance
            "lucky_mult",       // Lucky Card mult chance
            "misprint",         // Misprint joker random mult
            "bloodstone",       // Bloodstone probability
            "parking",          // Reserved Parking probability
            "business",         // Business Card probability
            "space",            // Space Joker probability
            "8ball",            // 8 Ball probability
            // "halu" - needs ante suffix, handled below
            "glass",            // Glass Card break chance
            
            // Boss blind effects (VERIFIED FROM LUA)
            "boss",             // Boss selection
            "wheel",            // The Wheel boss effect
            "hook",             // The Hook boss effect
            "cerulean_bell",    // Cerulean Bell boss effect
            "crimson_heart",    // Crimson Heart boss effect
            
            // Card modifications (VERIFIED FROM LUA)
            "wheel_of_fortune", // Wheel of Fortune tarot effect
            "invisible",        // Invisible Joker effect
            "perkeo",           // Perkeo consumable copy
            "madness",          // Madness joker destruction
            "ankh_choice",      // Ankh joker selection
            "to_do",            // To Do List poker hand selection
            "marb_fr",          // Marble Joker card creation
            "cert_fr",          // Certificate card front
            "certsl",           // Certificate card seal
            
            // Tarot/Spectral card effects (VERIFIED FROM LUA)
            "sigil",            // Sigil suit selection
            "ouija",            // Ouija rank selection
            "familiar_create",  // Familiar card creation
            "grim_create",      // Grim card creation
            "incantation_create", // Incantation card creation
            "random_destroy",   // Random card destruction
            "spe_card",         // Spectral card center selection
            "immolate",         // Immolate shuffle
            
            // Pack/shop generation (VERIFIED FROM LUA)
            "stdset",           // Standard pack enhancement chance (ante suffix)
            "stdseal",          // Standard pack seal chance (ante suffix)
            "stdsealtype",      // Standard pack seal type (ante suffix)
            "omen_globe",       // Omen Globe voucher effect
            "illusion",         // Illusion voucher effect
            "boss",             // Boss selection (no ante)
            "wheel",            // The Wheel boss effect (with pseudoseed)
            "hook",             // The Hook boss effect (with pseudoseed)
            "cerulean_bell",    // Cerulean Bell boss effect (with pseudoseed)
            "crimson_heart",    // Crimson Heart boss effect (with pseudoseed)
            "aura",             // Aura spectral effect
            "edition_generic",  // Generic edition poll
            "flipped_card",     // Flipped card chance
            "edition_deck",     // Erratic deck edition selection
            "erratic",          // Erratic suit/rank selection
            "orbital"           // Orbital Tag planet selection
        ]);
        
        // Keys that need ante suffix (1-8)
        for (int ante = 1; ante <= 8; ante++)
        {
            keys.Add($"halu{ante}");           // Hallucination probability
            keys.Add($"stdset{ante}");         // Standard pack enhancement
            keys.Add($"stdseal{ante}");        // Standard pack seal chance
            keys.Add($"stdsealtype{ante}");    // Standard pack seal type
            keys.Add($"cdt{ante}");            // Shop item type selection
            keys.Add($"rarity{ante}");         // Joker rarity roll
            keys.Add($"standard_edition{ante}"); // Standard pack edition
            keys.Add($"etperpoll{ante}");      // Shop eternal/perishable
            keys.Add($"packetper{ante}");      // Pack eternal/perishable
            keys.Add($"ssjr{ante}");           // Shop rental
            keys.Add($"packssjr{ante}");       // Pack rental
            keys.Add($"idol{ante}");           // The Idol card selection
            keys.Add($"mail{ante}");           // Mail-In Rebate card selection
            keys.Add($"anc{ante}");            // Ancient Joker suit selection
            keys.Add($"cas{ante}");            // Castle card selection
            
            // Soul keys for each type
            keys.Add($"soul_Tarot{ante}");
            keys.Add($"soul_Planet{ante}");
            keys.Add($"soul_Spectral{ante}");
            
            // Edition keys with source suffixes
            keys.Add($"ediar1{ante}");         // Arcana pack edition
            keys.Add($"edipl1{ante}");         // Celestial pack edition
            keys.Add($"edispe{ante}");         // Spectral pack edition
            keys.Add($"edista{ante}");         // Standard pack edition
            keys.Add($"edibuf{ante}");         // Buffoon pack edition
            keys.Add($"edisho{ante}");         // Shop edition
            
            // Front keys with source suffixes
            keys.Add($"frontar1{ante}");       // Arcana front
            keys.Add($"frontpl1{ante}");       // Celestial front
            keys.Add($"frontspe{ante}");       // Spectral front
            keys.Add($"frontsta{ante}");       // Standard front
            keys.Add($"frontbuf{ante}");       // Buffoon front
            keys.Add($"frontsho{ante}");       // Shop front
        }
        
        PseudoHashKeys = keys.ToArray();
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
                MotelyVectorPrngStream stream = searchContext.CreatePrngStream(key, true);
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
            return resultMask;
        }
    }
}
