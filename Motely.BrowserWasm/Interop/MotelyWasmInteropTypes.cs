namespace Motely.BrowserWasm.Interop;

public sealed record SearchProgressPayload(int InstanceId, long A, long B, long C);

public sealed record SearchResultPayload(int InstanceId, string Seed, int MatchCount);
