using Microsoft.AspNetCore.Diagnostics;

namespace CanDoItAll.IPFS.NodeControl.Services;

public static class NodeGatewayEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapNodeGatewayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/ipfs/{**gatewayPath}", (HttpContext httpContext, string? gatewayPath, NodeGatewayService nodeGatewayService, CancellationToken cancellationToken) =>
            HandleGatewayRequestAsync(httpContext, "ipfs", gatewayPath, nodeGatewayService, cancellationToken))
            .AllowAnonymous();
        endpoints.MapGet("/ipns/{**gatewayPath}", (HttpContext httpContext, string? gatewayPath, NodeGatewayService nodeGatewayService, CancellationToken cancellationToken) =>
            HandleGatewayRequestAsync(httpContext, "ipns", gatewayPath, nodeGatewayService, cancellationToken))
            .AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> HandleGatewayRequestAsync(
        HttpContext httpContext,
        string gatewayNamespace,
        string? gatewayPath,
        NodeGatewayService nodeGatewayService,
        CancellationToken cancellationToken)
    {
        var statusCodePagesFeature = httpContext.Features.Get<IStatusCodePagesFeature>();
        if (statusCodePagesFeature is not null)
        {
            statusCodePagesFeature.Enabled = false;
        }

        try
        {
            var resolution = await nodeGatewayService.ResolveAsync(
                gatewayNamespace,
                gatewayPath,
                httpContext.Request.Path.Value ?? string.Empty,
                httpContext.Request.QueryString.Value ?? string.Empty,
                cancellationToken).ConfigureAwait(false);

            return resolution.Kind switch
            {
                NodeGatewayResolutionKind.Redirect => BuildRedirectResult(httpContext, resolution),
                NodeGatewayResolutionKind.Html => BuildHtmlResult(httpContext, resolution),
                NodeGatewayResolutionKind.File => BuildFileResult(httpContext, resolution),
                NodeGatewayResolutionKind.NotFound => BuildNotFoundResult(httpContext, resolution),
                _ => Results.NotFound()
            };
        }
        catch (Exception ex) when (NodeGatewayService.IsNotFound(ex))
        {
            return Results.NotFound();
        }
    }

    private static IResult BuildFileResult(HttpContext httpContext, NodeGatewayResolution resolution)
    {
        resolution.ResponsePolicy.Apply(httpContext);
        httpContext.Response.RegisterForDispose(resolution);
        return Results.File(
            resolution.Stream!,
            resolution.ContentType ?? "application/octet-stream",
            fileDownloadName: null,
            lastModified: null,
            entityTag: resolution.ResponsePolicy.EntityTag,
            enableRangeProcessing: true);
    }

    private static IResult BuildHtmlResult(HttpContext httpContext, NodeGatewayResolution resolution)
    {
        resolution.ResponsePolicy.Apply(httpContext);
        return Results.Content(resolution.Html!, resolution.ContentType!);
    }

    private static IResult BuildNotFoundResult(HttpContext httpContext, NodeGatewayResolution resolution)
    {
        resolution.ResponsePolicy.Apply(httpContext);
        return Results.NotFound();
    }

    private static IResult BuildRedirectResult(HttpContext httpContext, NodeGatewayResolution resolution)
    {
        resolution.ResponsePolicy.Apply(httpContext);
        return Results.Redirect(resolution.RedirectLocation!);
    }
}
