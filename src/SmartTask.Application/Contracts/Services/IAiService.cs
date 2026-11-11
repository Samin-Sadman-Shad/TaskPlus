using SmartTask.Application.Common.Responses;
using SmartTask.Application.DTOs.Ai;

namespace SmartTask.Application.Contracts.Services;

/// <summary>Use case behind <c>AiController</c>.</summary>
public interface IAiService
{
    /// <summary>
    /// <c>POST ai/task-descriptions/improve</c>: 200 with the original text, the improved text and up to
    /// three suggestions; 502 when the model's output is unusable; 503 when the provider times out, is
    /// busy or is unavailable. Nothing is saved.
    /// </summary>
    Task<BaseResponse<ImproveDescriptionResponseDto>> ImproveDescriptionAsync(ImproveDescriptionRequestDto request, CancellationToken cancellationToken = default);
}
