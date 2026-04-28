using Yaaw.API.DTOs.Common;

namespace Yaaw.API.Services;

internal sealed class LinkService(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
{
    private HttpContext HttpContext =>
        httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP context.");

    public LinkDto Create(
        string endpointName,
        string rel,
        string method,
        object? values = null,
        string? controller = null)
    {
        string? href = controller is not null
            ? linkGenerator.GetUriByAction(HttpContext, endpointName, controller, values)
            : linkGenerator.GetUriByName(HttpContext, endpointName, values);

        return new LinkDto
        {
            Href = href ?? throw new InvalidOperationException(
                $"Could not generate URI for endpoint '{endpointName}'."),
            Rel = rel,
            Method = method,
        };
    }
}
