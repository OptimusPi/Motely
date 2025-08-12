
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System.Numerics;

namespace Motely;

public unsafe static class MotelyVectorUtils
{
    public static readonly bool IsAccelerated;
    public static readonly bool HasAvx512;
    public static readonly bool HasAvx2;
    public static readonly bool HasArmNeon;

    static MotelyVectorUtils()
    {
        // Check for platform-specific SIMD support
        HasAvx512 = Avx512F.IsSupported;
        HasAvx2 = Avx2.IsSupported;
        HasArmNeon = AdvSimd.IsSupported;
        
        // Accelerated if we have ANY SIMD support
        IsAccelerated = HasAvx512 || HasAvx2 || HasArmNeon || Vector256.IsHardwareAccelerated;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> ConvertToVector256Int32(in Vector512<double> vector)
    {
        if (HasAvx512)
        {
            // x86: Use AVX512 for best performance
            return Avx512F.ConvertToVector256Int32WithTruncation(vector);
        }
        else
        {
            // Fallback: Use portable SIMD narrow operations
            var lower = vector.GetLower();
            var upper = vector.GetUpper();
            
            // Convert doubles to floats first (narrows 64-bit to 32-bit)
            var floatLower = Vector128.Narrow(lower.GetLower(), lower.GetUpper());
            var floatUpper = Vector128.Narrow(upper.GetLower(), upper.GetUpper());
            
            // Then convert floats to ints with truncation
            var intLower = Vector128.ConvertToInt32(floatLower);
            var intUpper = Vector128.ConvertToInt32(floatUpper);
            
            return Vector256.Create(intLower, intUpper);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> ShiftLeft(in Vector256<int> value, in Vector256<int> shiftCount)
    {
        if (HasAvx2)
        {
            // x86: Use AVX2 variable shift
            return Avx2.ShiftLeftLogicalVariable(value, shiftCount.AsUInt32());
        }
        else if (HasArmNeon)
        {
            // ARM: NEON doesn't have variable shift for int32, use fallback
            // Fall through to portable implementation
            var s0 = shiftCount.GetElement(0) & 31;
            return value << s0; // Uses SIMD shift with broadcast
        }
        else
        {
            // Fallback: Use portable SIMD with constant shifts
            // This is still SIMD but less efficient than variable shift
            var s0 = shiftCount.GetElement(0) & 31;
            return value << s0; // Uses SIMD shift with broadcast
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<double> ExtendFloatMaskToDouble(in Vector256<float> smallMask)
        => Extend32MaskTo64<float, double>(smallMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<long> ExtendIntMaskToLong(in Vector256<int> smallMask)
        => Extend32MaskTo64<int, long>(smallMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<long> ExtendFloatMaskToLong(in Vector256<int> smallMask)
        => Extend32MaskTo64<int, long>(smallMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<double> ExtendIntMaskToDouble(in Vector256<int> smallMask)
        => Extend32MaskTo64<int, double>(smallMask);

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static Vector512<TTo> Extend32MaskTo64<TFrom, TTo>(in Vector256<TFrom> smallMask)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        if (sizeof(TFrom) != 4) throw new InvalidOperationException();
        if (sizeof(TTo) != 8) throw new InvalidOperationException();

        if (HasAvx2)
        {
            // x86: Use AVX2 sign extension
            Vector256<long> low = Avx2.ConvertToVector256Int64(smallMask.AsInt32().GetLower());
            Vector256<long> high = Avx2.ConvertToVector256Int64(smallMask.AsInt32().GetUpper());
            return Vector512.Create(low, high).As<long, TTo>();
        }
        else
        {
            // Portable SIMD: Use Vector.Widen for sign extension
            var input32 = smallMask.AsInt32();
            var lower128 = input32.GetLower();
            var upper128 = input32.GetUpper();
            
            // Widen operations are SIMD and preserve sign
            (var low0, var low1) = Vector128.Widen(lower128);
            (var high0, var high1) = Vector128.Widen(upper128);
            
            var low = Vector256.Create(low0, low1);
            var high = Vector256.Create(high0, high1);
            
            return Vector512.Create(low, high).As<long, TTo>();
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> ShrinkDoubleMaskToFloat(in Vector512<double> smallMask)
        => Shrink64MaskTo32<double, float>(smallMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> ShrinkLongMaskToFloat(in Vector512<long> smallMask)
        => Shrink64MaskTo32<long, float>(smallMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> ShrinkDoubleMaskToInt(in Vector512<double> smallMask)
        => Shrink64MaskTo32<double, int>(smallMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> ShrinkLongMaskToInt(in Vector512<long> smallMask)
        => Shrink64MaskTo32<long, int>(smallMask);

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static Vector256<TTo> Shrink64MaskTo32<TFrom, TTo>(in Vector512<TFrom> smallMask)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        if (sizeof(TTo) != 4) throw new InvalidOperationException();
        if (sizeof(TFrom) != 8) throw new InvalidOperationException();

        if (HasAvx512)
        {
            // x86: Use AVX512 conversion
            return Avx512F.ConvertToVector256Int32(smallMask.AsUInt64()).As<int, TTo>();
        }
        else
        {
            // Portable SIMD: Use vector narrow operations
            var input64 = smallMask.AsInt64();
            var lower = input64.GetLower();
            var upper = input64.GetUpper();
            
            // Narrow using SIMD operations
            var narrowLower = Vector128.Narrow(lower.GetLower(), lower.GetUpper());
            var narrowUpper = Vector128.Narrow(upper.GetLower(), upper.GetUpper());
            
            return Vector256.Create(narrowLower, narrowUpper).As<int, TTo>();
        }
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static uint VectorMaskToIntMask<T>(in Vector256<T> vector) where T : unmanaged
    {
        if (sizeof(T) != 4) throw new InvalidOperationException();
        return Vector256.ExtractMostSignificantBits(vector);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static uint VectorMaskToIntMask<T>(in Vector512<T> vector) where T : unmanaged
    {
        if (sizeof(T) != 8) throw new InvalidOperationException();
        return (uint)Vector512.ExtractMostSignificantBits(vector);
    }
}
