using CanDoItAll.Components;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.IPFS.DesktopHost;
using CanDoItAll.IPFS.NodeControl.Abstractions;
using CanDoItAll.IPFS.NodeControl.Composition;
using CanDoItAll.IPFS.NodeControl.Components;
using CanDoItAll.IPFS.NodeControl.Models;
using CanDoItAll.IPFS.NodeControl.Options;
using CanDoItAll.IPFS.NodeControl.Security;
using CanDoItAll.IPFS.NodeControl.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var runningFromSourceTree = Directory.Exists(Path.Combine(builder.Environment.ContentRootPath, "obj"));
// Source-tree runs need the static web assets manifest from obj/, but published outputs
// already contain their final wwwroot payload and should not re-enable source manifests.
if (runningFromSourceTree)
{
    builder.WebHost.UseStaticWebAssets();
}

// Add services to the container.
builder.Services.AddIpfsNodeControlApplication(builder.Configuration);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
var contentTypeProvider = new FileExtensionContentTypeProvider();
var publishedStaticAssets = runningFromSourceTree
    ? null
    : PublishedStaticAssetManifest.TryLoad(AppContext.BaseDirectory);
var operatingProfileOptions = app.Services.GetRequiredService<IOptions<OperatingProfileOptions>>().Value;
var applyRateLimiting = operatingProfileOptions.EnableRateLimiting == true;

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseForwardedHeaders();
app.UseHttpsRedirection();

if (publishedStaticAssets is not null)
{
    app.Use(async (context, next) =>
    {
        if (await publishedStaticAssets.TryWriteResponseAsync(context).ConfigureAwait(false))
        {
            return;
        }

        await next().ConfigureAwait(false);
    });
}

app.Use(async (context, next) =>
{
    var statusCodePagesFeature = context.Features.Get<IStatusCodePagesFeature>();
    if (statusCodePagesFeature is not null
        && (context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.StartsWithSegments("/health")))
    {
        statusCodePagesFeature.Enabled = false;
    }

    await next().ConfigureAwait(false);
});

app.UseMiddleware<NodeControlCorrelationMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
if (publishedStaticAssets is null)
{
    app.MapStaticAssets();
}
var liveHealth = app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = NodeControlHealthCheckResponseWriter.WriteJsonAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});
var readyHealth = app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = NodeControlHealthCheckResponseWriter.WriteJsonAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});
if (operatingProfileOptions.RequireAdminAuthentication == true)
{
    readyHealth.RequireAuthorization(ControlAppAuthorizationPolicyNames.AdminApi);
}
app.MapNodeGatewayEndpoints();
var adminApi = app.MapGroup("/api")
    .RequireAuthorization(ControlAppAuthorizationPolicyNames.AdminApi);
if (applyRateLimiting)
{
    adminApi.RequireRateLimiting(ControlAppRateLimitPolicyNames.AdminApi);
}

var remotePinApi = app.MapGroup("/api/remote-pin")
    .RequireAuthorization(ControlAppAuthorizationPolicyNames.RemotePinIngress);
if (applyRateLimiting)
{
    remotePinApi.RequireRateLimiting(ControlAppRateLimitPolicyNames.RemotePinIngress);
}

adminApi.MapGet("/files/content", async Task<IResult> (
    HttpContext httpContext,
    string path,
    string? name,
    bool download,
    INodeConnectionLeaseFactory leaseFactory,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return Results.BadRequest("A file path or CID is required.");
    }

    var lease = await leaseFactory.CreateLeaseAsync(
        NodeConnectionRequestCategory.ReadOnlyUi,
        cancellationToken).ConfigureAwait(false);
    try
    {
        var stream = await lease.Client.FileSystem.ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
        httpContext.Response.RegisterForDispose(lease);
        httpContext.Response.RegisterForDispose(stream);

        var fileName = ResolveFileName(path, name, download);
        var contentType = ResolveContentType(fileName, contentTypeProvider);
        return Results.File(stream, contentType, download ? fileName : null, enableRangeProcessing: true);
    }
    catch
    {
        lease.Dispose();
        throw;
    }
});
adminApi.MapPost("/files/upload-browser", async Task<IResult> (
    HttpRequest request,
    bool pin,
    bool wrap,
    NodeOperatorService nodeOperatorService,
    CancellationToken cancellationToken) =>
{
    var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
    if (form.Files.Count == 0 && form["dir"].Count == 0 && form["rootName"].Count == 0)
    {
        return Results.BadRequest("Select a file or folder to upload.");
    }

    await using var staging = await BrowserUploadStaging.CreateAsync(form, cancellationToken).ConfigureAwait(false);
    var snapshot = staging.IsSingleFile
        ? await nodeOperatorService.UploadLocalFileAsync(staging.SingleFilePath!, pin, wrap, cancellationToken).ConfigureAwait(false)
        : await nodeOperatorService.UploadLocalDirectoryAsync(staging.UploadRootPath, pin, cancellationToken).ConfigureAwait(false);

    return Results.Json(snapshot);
});
adminApi.MapGet("/logs", (
    string? window,
    int? limit,
    IApplicationLogStore applicationLogStore) =>
{
    var slice = applicationLogStore.ReadRecent(window, limit);
    return Results.Json(slice);
});
adminApi.MapGet("/logs/download", (
    string? window,
    int? limit,
    IApplicationLogStore applicationLogStore) =>
{
    var slice = applicationLogStore.ReadRecent(window, limit);
    var bytes = applicationLogStore.BuildPlainTextSlice(slice);
    var fileName = $"candoitall-ipfs-node-control-{slice.WindowKey}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log";
    return Results.File(bytes, "text/plain; charset=utf-8", fileName);
});
remotePinApi.MapGet("/probe", async Task<IResult> (
    ConfiguredNodeStatusService configuredNodeStatusService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var snapshot = await configuredNodeStatusService.GetReceiverProbeAsync(cancellationToken).ConfigureAwait(false);
        return Results.Json(snapshot);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Remote pin receiver probe failed.");
        var failedSnapshot = configuredNodeStatusService.BuildFailedProbeSnapshot(ex.Message);
        return Results.Json(failedSnapshot, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
remotePinApi.MapPost("/requests", Task<IResult> (
    RemotePinRequestEnvelope request,
    RemotePinRequestWorkflowService remotePinRequestWorkflowService) =>
{
    try
    {
        var stored = remotePinRequestWorkflowService.Enqueue(request);
        return Task.FromResult<IResult>(Results.Json(stored, statusCode: StatusCodes.Status202Accepted));
    }
    catch (ArgumentException ex)
    {
        app.Logger.LogWarning(ex, "Remote pin request enqueue failed for request {RequestId}.", request.RequestId);
        return Task.FromResult<IResult>(Results.BadRequest(ex.Message));
    }
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var hostedUrlRegistry = app.Services.GetRequiredService<HostedUrlRegistry>();
app.Lifetime.ApplicationStarted.Register(() =>
{
    var server = app.Services.GetRequiredService<IServer>();
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
    hostedUrlRegistry.Update(addresses);
});

app.Run();

static string ResolveFileName(string path, string? name, bool download)
{
    var requestedName = string.IsNullOrWhiteSpace(name)
        ? string.Empty
        : Path.GetFileName(name.Trim());
    if (!download && !string.IsNullOrWhiteSpace(requestedName))
    {
        return requestedName;
    }

    var trimmedPath = path.Trim();
    var lastSlash = trimmedPath.LastIndexOf('/');
    var baseName = lastSlash >= 0 && lastSlash < trimmedPath.Length - 1
        ? trimmedPath[(lastSlash + 1)..]
        : trimmedPath;

    if (!download)
    {
        return baseName;
    }

    var extension = Path.GetExtension(requestedName);
    return string.IsNullOrWhiteSpace(extension)
        ? baseName
        : $"{baseName}{extension}";
}

static string ResolveContentType(string fileName, FileExtensionContentTypeProvider provider)
    => provider.TryGetContentType(fileName, out var contentType)
        ? contentType
        : "application/octet-stream";

sealed class BrowserUploadStaging(string stagingRoot, string uploadRootPath, string? singleFilePath, bool isSingleFile) : IAsyncDisposable
{
    public string UploadRootPath { get; } = uploadRootPath;

    public string? SingleFilePath { get; } = singleFilePath;

    public bool IsSingleFile { get; } = isSingleFile;

    public static async Task<BrowserUploadStaging> CreateAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"browser-upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);

        var rootName = form["rootName"].FirstOrDefault();
        var uploadRootPath = string.IsNullOrWhiteSpace(rootName)
            ? stagingRoot
            : Path.Combine(stagingRoot, SanitizeRootName(rootName));
        Directory.CreateDirectory(uploadRootPath);

        var directoryHints = form["dir"]
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeRelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var directoryHint in directoryHints)
        {
            Directory.CreateDirectory(CombineWithinRoot(uploadRootPath, directoryHint));
        }

        string? firstFilePath = null;
        var hasNestedPath = false;
        foreach (var file in form.Files)
        {
            var relativePath = NormalizeRelativePath(file.FileName);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException("Each uploaded file must include a file name.");
            }

            hasNestedPath |= relativePath.Contains(Path.DirectorySeparatorChar) || relativePath.Contains(Path.AltDirectorySeparatorChar);
            var fullPath = CombineWithinRoot(uploadRootPath, relativePath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            await using var input = file.OpenReadStream();
            await using var output = File.Create(fullPath);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            firstFilePath ??= fullPath;
        }

        var isSingleFile = string.IsNullOrWhiteSpace(rootName)
            && directoryHints.Count == 0
            && form.Files.Count == 1
            && !hasNestedPath
            && firstFilePath is not null;

        return new BrowserUploadStaging(stagingRoot, uploadRootPath, firstFilePath, isSingleFile);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore transient cleanup failures for staged browser uploads.
        }

        return ValueTask.CompletedTask;
    }

    private static string SanitizeRootName(string? rootName)
    {
        var normalized = NormalizeRelativePath(rootName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "upload";
        }

        var segments = normalized.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? "upload" : segments[segments.Length - 1];
    }

    private static string NormalizeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim().Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == "." || segment == ".."))
        {
            throw new InvalidOperationException("Uploaded paths must stay within the selected folder.");
        }

        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    private static string CombineWithinRoot(string root, string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(root, relativePath));
        var normalizedRoot = Path.GetFullPath(root);

        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Uploaded paths must stay within the selected folder.");
        }

        return combined;
    }
}
