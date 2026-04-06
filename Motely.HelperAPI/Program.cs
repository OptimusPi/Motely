using Microsoft.Extensions.Options;
using Motely;
using Motely.DistributedWorker;

var builder = WebApplication.CreateBuilder(args);

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

// Serve static files from wwwroot/ (root-level static assets).
app.UseDefaultFiles();
app.UseStaticFiles();

// Serve /jammy-seed-finder/ with unknown file types (WASM .dll, .br, .gz, etc.)
app.UseDefaultFiles(new DefaultFilesOptions
{
    RequestPath = "/jammy-seed-finder",
    DefaultFileNames = ["index.html"]
});
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/jammy-seed-finder",
    ServeUnknownFileTypes = true
});

// ── Routes ──────────────────────────────────────────────────────────

var motelyVersion = typeof(MotelyGlobals).Assembly.GetName().Version?.ToString(3) ?? "7.0.0";

app.MapGet("/", () => Results.Redirect("/jammy-seed-finder/"));

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
