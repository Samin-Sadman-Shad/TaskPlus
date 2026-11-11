using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartTask.Api.RateLimiting;
using SmartTask.Application.Common.Responses;
using SmartTask.Application.Contracts.Services;
using SmartTask.Application.DTOs.Ai;

namespace SmartTask.Api.Controllers;

/// <summary>AI assistance. Any signed-in user may call it; it never saves anything.</summary>
[Route("api/v1/ai")]
[ProducesResponseType<BaseResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<BaseResponse>(StatusCodes.Status429TooManyRequests)]
public sealed class AiController(IAiService aiService) : BaseApiController
{
    /// <summary>
    /// Rewrites a rough task description so it is grammatically correct, clearer, more professional and
    /// actionable, and expands it when it is very short. Returns the original, the suggested text and up
    /// to three notes about missing information. Nothing is saved: the client decides whether to apply it.
    /// The text is sent to a third-party AI provider.
    /// </summary>
    /// <response code="502">The AI provider returned output that failed validation.</response>
    /// <response code="503">The AI provider timed out, is busy or is unavailable.</response>
    [HttpPost("task-descriptions/improve")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [ProducesResponseType<BaseResponse<ImproveDescriptionResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<BaseResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<BaseResponse<ImproveDescriptionResponseDto>>> ImproveDescription(
        ImproveDescriptionRequestDto request, CancellationToken cancellationToken)
        => ToActionResult(await aiService.ImproveDescriptionAsync(request, cancellationToken));
}
