# .NET SIMD Memory Alignment & stackalloc Guide

## Why Memory Alignment Matters
- SIMD instructions (Vector256/Vector512) are fastest when data is aligned to their natural boundary (32/64 bytes).
- .NET's stackalloc and arrays are generally aligned, but pinning or native interop may require explicit alignment.
- Misaligned loads/stores can cause performance penalties or exceptions on some hardware.

## Using stackalloc for Temp Buffers
- Use `stackalloc` for small, short-lived buffers in hot paths:
  ```csharp
  Span<double> buffer = stackalloc double[Vector512<double>.Count];
  // Use buffer for SIMD operations
  ```
- For unsafe code:
  ```csharp
  double* buffer = stackalloc double[Vector512<double>.Count];
  // Use with intrinsics or pointers
  ```
- stackalloc is always stack-allocated, zero-GC, and fast.

## Ensuring Alignment
- .NET stackalloc is naturally aligned for primitive types.
- For arrays, prefer stackalloc or use `MemoryMarshal.GetArrayDataReference()` for best alignment.
- For native interop, use `Marshal.AllocHGlobal` and align manually if needed.
- For Span<T> over native memory, use `MemoryMarshal.CreateSpan(ref, length)`.

## Best Practices
- Use stackalloc for all temp SIMD buffers in hot paths.
- Use Span<T> or ref struct to pass buffers without allocations.
- Avoid heap allocations for per-iteration temp data.
- For large buffers, use ArrayPool<T>.Shared.Rent/Return.

## Example: SIMD Batch Processing
```csharp
Span<double> batch = stackalloc double[Vector512<double>.Count];
for (int i = 0; i < batch.Length; i++)
    batch[i] = ...;
var vec = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(batch));
```

## Further Reading
- [.NET SIMD Intrinsics](https://learn.microsoft.com/dotnet/api/system.runtime.intrinsics)
- [Span<T> and stackalloc](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/stackalloc)
- [Memory alignment in .NET](https://learn.microsoft.com/dotnet/standard/native-interop/memory-alignment)
