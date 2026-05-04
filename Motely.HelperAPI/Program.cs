using System.Reflection;
using Microsoft.AspNetCore.StaticFiles;
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

// ── MIME type provider — WASM needs application/wasm or the browser
//    will refuse to compile it. Also explicitly map .mjs, .br, .gz.
var mimeProvider = new FileExtensionContentTypeProvider();
mimeProvider.Mappings[".wasm"]  = "application/wasm";
mimeProvider.Mappings[".mjs"]   = "text/javascript";
mimeProvider.Mappings[".cjs"]   = "text/javascript";
mimeProvider.Mappings[".br"]    = "application/x-brotli";
mimeProvider.Mappings[".gz"]    = "application/x-gzip";
mimeProvider.Mappings[".blat"]  = "application/octet-stream";
mimeProvider.Mappings[".dat"]   = "application/octet-stream";
mimeProvider.Mappings[".sym"]   = "application/octet-stream";

var staticFileOptions = new StaticFileOptions
{
    ContentTypeProvider = mimeProvider,
    ServeUnknownFileTypes = true,  // Catch any edge-case extensions (pdb, etc.)
    // Enable headers that let pre-compressed files be served correctly.
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        if (path.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.ContentEncoding = "br";
            // Strip the .br so the browser gets the original content-type
            var baseName = path[..^3];
            if (mimeProvider.TryGetContentType(baseName, out var baseMime))
                ctx.Context.Response.ContentType = baseMime;
        }
        else if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.ContentEncoding = "gzip";
            var baseName = path[..^3];
            if (mimeProvider.TryGetContentType(baseName, out var baseMime))
                ctx.Context.Response.ContentType = baseMime;
        }
        // Wide-open COOP/COEP for SharedArrayBuffer (threads support)
        ctx.Context.Response.Headers["Cross-Origin-Opener-Policy"]    = "same-origin";
        ctx.Context.Response.Headers["Cross-Origin-Embedder-Policy"]  = "require-corp";
    },
};

// ── Serve the Avalonia WASM app at /jammy-seed-finder/ ──────────────
// UseDefaultFiles + UseStaticFiles must be scoped to the sub-path so
// the browser gets the right index.html (the Avalonia bootstrap) and
// not the root splash page.
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "jammy-seed-finder")),
    RequestPath = "/jammy-seed-finder",
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "jammy-seed-finder")),
    RequestPath = "/jammy-seed-finder",
    ContentTypeProvider = mimeProvider,
    ServeUnknownFileTypes = true,
    OnPrepareResponse = staticFileOptions.OnPrepareResponse,
});

// ── Root wwwroot (splash page, duckdb-reader.js, assorted assets) ───
app.UseDefaultFiles();
app.UseStaticFiles(staticFileOptions);

// ── Routes ──────────────────────────────────────────────────────────

// Motely.dll has no product version (GenerateAssemblyInfo=false). Expose repo MotelyVersion via *this* assembly (GenerateAssemblyInfo=true + Directory.Build.props Version).
var motelyVersion = GetHelperApiMotelyVersion();

static string GetHelperApiMotelyVersion()
{
    var asm = Assembly.GetExecutingAssembly();
    var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
    if (info?.InformationalVersion is { Length: > 0 } s)
        return s.Split('+')[0].Trim();
    return asm.GetName().Version?.ToString(3) ?? "7.0.0";
}

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
