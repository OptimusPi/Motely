using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public static unsafe class VectorEnum
{
    public static VectorEnum<T> Create<T>(T value)
        where T : unmanaged, Enum
    {
        return new(SimdIntResult.Create(Unsafe.As<T, int>(ref value)));
    }

    public static VectorEnum<T> Create<T>(SimdIntResult indices, T[] values)
        where T : unmanaged, Enum
    {
        int* vector = stackalloc int[SimdIntResult.Count];

        for (int i = 0; i < SimdIntResult.Count; i++)
        {
            if (i < Motely.MaxVectorWidth)
            {
                vector[i] = Unsafe.As<T, int>(ref values[indices[i]]);
            }
            else
            {
                vector[i] = 0; // Safe default
            }
        }

        // Load from the stack buffer (span)
        return new(SimdIntResult.Load(vector));
    }

    public static SimdIntResult Equals<T>(in VectorEnum<T> a, T b)
        where T : unmanaged, Enum
    {
        return SimdIntResult.Equals(
            a.HardwareVector,
            SimdIntResult.Create(Unsafe.As<T, int>(ref b))
        );
    }

    public static SimdIntResult Equals<T>(in VectorEnum<T> a, in VectorEnum<T> b)
        where T : unmanaged, Enum
    {
        return SimdIntResult.Equals(a.HardwareVector, b.HardwareVector);
    }
}

public unsafe struct VectorEnum<T>(SimdIntResult hardwareVector)
    where T : unmanaged, Enum
{
    public SimdIntResult HardwareVector = hardwareVector;

    static VectorEnum()
    {
        if (sizeof(T) != 4)
            throw new ArgumentException($"Size of {nameof(T)} must be 4 bytes.");
    }

    public readonly T this[int i]
    {
        get
        {
            int value = HardwareVector[i];
            return Unsafe.As<int, T>(ref value);
        }
    }

    public override string ToString()
    {
        // ToString limited to MaxVectorWidth
        var sb = new System.Text.StringBuilder("<");
        for (int i = 0; i < Motely.MaxVectorWidth; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(this[i]);
        }
        sb.Append('>');
        return sb.ToString();
    }

    public static implicit operator VectorEnum<T>(VectorEnum256<T> v) =>
        new(new SimdIntResult(v.HardwareVector));

    public static implicit operator VectorEnum256<T>(VectorEnum<T> v) =>
        new(v.HardwareVector.Value);
}
