using System.Runtime.CompilerServices;

namespace Motely;

ref partial struct MotelySeedContext
{
    private static readonly MotelyTag[] SeedContextDisallowedAnteOneTags =
    [
        MotelyTag.NegativeTag,
        MotelyTag.StandardTag,
        MotelyTag.MeteorTag,
        MotelyTag.BuffoonTag,
        MotelyTag.HandyTag,
        MotelyTag.GarbageTag,
        MotelyTag.EtherealTag,
        MotelyTag.TopupTag,
        MotelyTag.OrbitalTag,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelySingleTagStream CreateTagStream(int ante, bool isCached = false)
    {
        return new(CreateResampleStream(MotelyPrngKeys.Tags + ante, isCached), ante);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyTag GetNextTag(ref MotelySingleTagStream tagStream)
    {
        if (tagStream.Ante > 1)
        {
            return (MotelyTag)GetNextRandomInt(
                ref tagStream.ResampleStream.InitialPrngStream,
                0,
                MotelyEnum<MotelyTag>.ValueCount
            );
        }

        MotelyTag tag = (MotelyTag)GetNextRandomInt(
            ref tagStream.ResampleStream.InitialPrngStream,
            0,
            MotelyEnum<MotelyTag>.ValueCount
        );

        int resampleCount = 0;

        while (SeedContextDisallowedAnteOneTags.Contains(tag))
        {
            tag = (MotelyTag)GetNextRandomInt(
                ref GetResamplePrngStream(
                    ref tagStream.ResampleStream,
                    MotelyPrngKeys.Tags + tagStream.Ante,
                    resampleCount
                ),
                0,
                MotelyEnum<MotelyTag>.ValueCount
            );

            ++resampleCount;
        }

        return tag;
    }
}
