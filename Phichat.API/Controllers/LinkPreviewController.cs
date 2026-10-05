using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Phichat.API.Security;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.LinkPreview;

namespace Phichat.API.Controllers;

/// <summary>Link previews for the sender's client (recipients get the preview inside the encrypted message).</summary>
[ApiController]
[Route("api/link-preview")]
[Authorize]
public class LinkPreviewController : ControllerBase
{
    private readonly ILinkPreviewService _previews;

    public LinkPreviewController(ILinkPreviewService previews)
    {
        _previews = previews;
    }

    public class LinkPreviewRequest
    {
        public string Url { get; set; } = "";
    }

    /// <summary>POST so the link stays in the body: request logs record paths and query strings.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.LinkPreview)]
    public async Task<IActionResult> Fetch([FromBody] LinkPreviewRequest request, CancellationToken cancellationToken)
    {
        if (LinkPreviewService.NormalizeUrl(request.Url) == null)
            throw new BadRequestException("invalid_url", "Only http and https links can be previewed.");

        var preview = await _previews.GetAsync(request.Url, cancellationToken);
        return preview == null ? NoContent() : Ok(preview);
    }
}
