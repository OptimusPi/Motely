using System.Text.Json.Serialization;

namespace Motely.HelperAPI;

[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(VersionResponse))]
[JsonSerializable(typeof(WorkerStatusResponse))]
[JsonSerializable(typeof(FilterDto))]
[JsonSerializable(typeof(List<FilterDto>))]
[JsonSerializable(typeof(StartSearchRequest))]
[JsonSerializable(typeof(StartSearchResponse))]
[JsonSerializable(typeof(StopSearchResponse))]
[JsonSerializable(typeof(ValidateRequest))]
[JsonSerializable(typeof(ValidateResponse))]
[JsonSerializable(typeof(SearchJobDto))]
[JsonSerializable(typeof(List<SearchJobDto>))]
[JsonSerializable(typeof(JobResultsDto))]
[JsonSerializable(typeof(FoundSeedDto))]
[JsonSerializable(typeof(ErrorResponse))]
internal partial class HelperApiJsonContext : JsonSerializerContext;
