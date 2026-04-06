using Microsoft.Extensions.Options;
using Motely;
using Motely.DistributedWorker;
using Motely.HelperAPI;

var builder = WebApplication.CreateBuilder(args);

// ── AOT-safe JSON ───────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Add(HelperApiJsonContext.Default));

// ── CORS — open for community tools ─────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Distributed Worker (in-process) ─────────────────────────────────
builder.Services.Configure<PoolWorkerOptions>(
    builder.Configuration.GetSection(PoolWorkerOptions.SectionName));
builder.Services.AddHostedService<PoolWorkerHostedService>();

var app = builder.Build();

// ── Middleware pipeline ─────────────────────────────────────────────

app.UseCors();

// Serve the motely-wasm seed tools (Vite build output copied to wwwroot/).
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true // .wasm, .mjs, etc.
});

// ── Routes ──────────────────────────────────────────────────────────

var motelyVersion = typeof(MotelyGlobals).Assembly.GetName().Version?.ToString(3) ?? "7.0.0";

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", motelyVersion)));

app.MapGet("/api/version", () =>
    Results.Ok(new VersionResponse(
        Api: "Motely.HelperAPI",
        Motely: motelyVersion,
        Runtime: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
    )));

app.MapGet("/api/worker/status", (IOptions<PoolWorkerOptions> opts) =>
{
    var o = opts.Value;
    return Results.Ok(new WorkerStatusResponse(
        Enabled: !string.IsNullOrWhiteSpace(o.Url),
        PoolUrl: o.Url,
        Threads: o.Threads,
        WorkerId: o.WorkerId,
        FilterId: o.FilterId
    ));
});

app.Run();

// ── Response records ────────────────────────────────────────────────

record HealthResponse(string Status, string MotelyVersion);
record VersionResponse(string Api, string Motely, string Runtime);
record WorkerStatusResponse(bool Enabled, string PoolUrl, int Threads, string WorkerId, string FilterId);
