using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public struct SimdIntResult
{
    public Vector256<int> Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimdIntResult(Vector256<int> value)
    {
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SimdIntResult Create(int value)
    {
        return new SimdIntResult(Vector256.Create(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SimdIntResult(Vector256<int> value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector256<int>(SimdIntResult result) => result.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SimdIntResult operator +(SimdIntResult left, SimdIntResult right)
    {
        return new SimdIntResult(Vector256.Add(left.Value, right.Value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SimdIntResult operator +(Vector256<int> left, SimdIntResult right)
    {
        return new SimdIntResult(Vector256.Add(left, right.Value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SimdIntResult operator +(SimdIntResult left, Vector256<int> right)
    {
        return new SimdIntResult(Vector256.Add(left.Value, right));
    }

    public const int Count = 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe SimdIntResult Load(int* address)
    {
        return new SimdIntResult(Vector256.Load(address));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SimdIntResult Equals(SimdIntResult left, SimdIntResult right)
    {
        return new SimdIntResult(Vector256.Equals(left.Value, right.Value));
    }

    public readonly int this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return Value.GetElement(index);
        }
    }
}
