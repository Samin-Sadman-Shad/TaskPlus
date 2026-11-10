using System.Net;

namespace SmartTask.Application.Common.Responses;

/// <summary>
/// The response shape every endpoint returns, errors included. It uses only
/// <see cref="HttpStatusCode"/>, so the Application layer has no ASP.NET Core dependency.
/// </summary>
public interface IBaseResponse
{
    HttpStatusCode Status { get; }

    bool Success { get; }

    string? Message { get; }

    IReadOnlyDictionary<string, string[]>? Errors { get; }

    /// <summary>Set by <c>BaseApiController</c> or the exception handler from <c>HttpContext.TraceIdentifier</c>.</summary>
    string? TraceId { get; set; }
}
