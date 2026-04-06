using System.Text.Json.Serialization;

namespace Motely.HelperAPI;

[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(VersionResponse))]
[JsonSerializable(typeof(WorkerStatusResponse))]
internal partial class HelperApiJsonContext : JsonSerializerContext;
