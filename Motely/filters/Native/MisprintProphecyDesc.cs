using System.Runtime.Intrinsics;

namespace Motely;

/// <summary>
/// "The Misprint Prophecy" - Detects seeds where Misprint joker's random mult values
/// form extraordinary sequences that humans would never notice.
/// 
/// Misprint generates mult values 0-23 uniformly at random per hand played.
/// This filter finds seeds where those values form patterns:
/// - Ascending: 1, 2, 3, 4, 5... (like a countdown to victory)
/// - Descending: 23, 22, 21, 20... (the inevitable approach)
/// - Constant: Same value N times in a row (the universe speaking)
/// - Palindrome: Values that read the same forwards and backwards
/// 
/// These patterns are invisible to players who just see "random" numbers,
/// but the PRNG is deterministic - the prophecy was always written.
/// </summary>
public struct MisprintProphecyFilterDesc : IMotelySeedFilterDesc<MisprintProphecyFilterDesc.MisprintProphecyFilter>
{
    public enum ProphecyType
    {
        /// <summary>Mult values count up: 1, 2, 3, 4, 5...</summary>
        Ascending,
        /// <summary>Mult values count down: 23, 22, 21, 20...</summary>
        Descending,
        /// <summary>All mult values are identical: X, X, X, X...</summary>
        Constant,
        /// <summary>Mult values form a palindrome: 5, 10, 15, 10, 5</summary>
        Palindrome,
        /// <summary>Mult values are all lucky 7s: 7, 7, 7, 7...</summary>
        LuckySeven,
        /// <summary>Mult values are all 23 (maximum): 23, 23, 23...</summary>
        Perfect23,
    }

    public ProphecyType Type { get; init; } = ProphecyType.Ascending;
    public int SequenceLength { get; init; } = 5;
    public int? TargetValue { get; init; } = null; // For Constant type, the value to match

    public MisprintProphecyFilterDesc() { }

    public readonly MisprintProphecyFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new MisprintProphecyFilter(Type, SequenceLength, TargetValue);
    }

    public struct MisprintProphecyFilter : IMotelySeedFilter
    {
        private readonly ProphecyType _type;
        private readonly int _sequenceLength;
        private readonly int? _targetValue;

        public MisprintProphecyFilter(ProphecyType type, int sequenceLength, int? targetValue)
        {
            _type = type;
            _sequenceLength = sequenceLength;
            _targetValue = targetValue;
        }

        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var stream = ctx.CreateMisprintPrngStream();

            // We need to track mult values across hands
            // Misprint mult is 0-23, so we read _sequenceLength values
            // and check if they match the pattern

            Span<int> mults = stackalloc int[_sequenceLength];

            for (int hand = 0; hand < _sequenceLength; hand++)
            {
                // Get the next Misprint mult value for each lane
                var multVector = ctx.GetNextMisprintMult(ref stream);

                // We'll check each lane individually in the mask
                // For now, store the first lane's value for pattern checking
                // The actual vectorized check happens below
            }

            // Re-create stream for actual vectorized filtering
            stream = ctx.CreateMisprintPrngStream();

            return _type switch
            {
                ProphecyType.Ascending => CheckAscending(ref ctx, ref stream),
                ProphecyType.Descending => CheckDescending(ref ctx, ref stream),
                ProphecyType.Constant => CheckConstant(ref ctx, ref stream, _targetValue ?? 7),
                ProphecyType.Palindrome => CheckPalindrome(ref ctx, ref stream),
                ProphecyType.LuckySeven => CheckConstant(ref ctx, ref stream, 7),
                ProphecyType.Perfect23 => CheckConstant(ref ctx, ref stream, 23),
                _ => VectorMask.NoBitsSet
            };
        }

        private readonly VectorMask CheckAscending(ref MotelyVectorSearchContext ctx, ref MotelyVectorPrngStream stream)
        {
            // Check if mults form 1, 2, 3, 4, 5... (starting from 1)
            VectorMask resultMask = VectorMask.AllBitsSet;

            for (int i = 0; i < _sequenceLength; i++)
            {
                var multVector = ctx.GetNextMisprintMult(ref stream);
                var expected = Vector256.Create(i + 1); // 1, 2, 3, 4, 5...
                var matches = Vector256.Equals(multVector, expected);
                resultMask &= new VectorMask(Vector256.ExtractMostSignificantBits(matches));

                if (resultMask.IsAllFalse())
                    break;
            }

            return resultMask;
        }

        private readonly VectorMask CheckDescending(ref MotelyVectorSearchContext ctx, ref MotelyVectorPrngStream stream)
        {
            // Check if mults form 23, 22, 21, 20... (starting from 23)
            VectorMask resultMask = VectorMask.AllBitsSet;

            for (int i = 0; i < _sequenceLength; i++)
            {
                var multVector = ctx.GetNextMisprintMult(ref stream);
                var expected = Vector256.Create(23 - i); // 23, 22, 21, 20...
                var matches = Vector256.Equals(multVector, expected);
                resultMask &= new VectorMask(Vector256.ExtractMostSignificantBits(matches));

                if (resultMask.IsAllFalse())
                    break;
            }

            return resultMask;
        }

        private readonly VectorMask CheckConstant(ref MotelyVectorSearchContext ctx, ref MotelyVectorPrngStream stream, int value)
        {
            // Check if all mults are the same value
            VectorMask resultMask = VectorMask.AllBitsSet;
            var expected = Vector256.Create(value);

            for (int i = 0; i < _sequenceLength; i++)
            {
                var multVector = ctx.GetNextMisprintMult(ref stream);
                var matches = Vector256.Equals(multVector, expected);
                resultMask &= new VectorMask(Vector256.ExtractMostSignificantBits(matches));

                if (resultMask.IsAllFalse())
                    break;
            }

            return resultMask;
        }

        private readonly VectorMask CheckPalindrome(ref MotelyVectorSearchContext ctx, ref MotelyVectorPrngStream stream)
        {
            // For palindrome, we need an odd-length sequence: A, B, C, B, A
            // We'll use _sequenceLength values and check symmetry
            // Minimum length for meaningful palindrome is 3: A, B, A

            int len = Math.Max(3, _sequenceLength);
            if (len % 2 == 0) len++; // Make odd for true palindrome

            // Read all values first
            Span<Vector256<int>> mults = stackalloc Vector256<int>[len];
            for (int i = 0; i < len; i++)
            {
                mults[i] = ctx.GetNextMisprintMult(ref stream);
            }

            // Check palindrome property: mults[i] == mults[len-1-i]
            VectorMask resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < len / 2; i++)
            {
                var matches = Vector256.Equals(mults[i], mults[len - 1 - i]);
                resultMask &= new VectorMask(Vector256.ExtractMostSignificantBits(matches));

                if (resultMask.IsAllFalse())
                    break;
            }

            return resultMask;
        }
    }
}
