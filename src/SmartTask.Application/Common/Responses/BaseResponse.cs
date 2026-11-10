using System.Net;
using System.Text.Json.Serialization;

namespace SmartTask.Application.Common.Responses;

/// <summary>
/// Response without a payload (delete, logout, membership changes, and every failure). Create
/// instances only through the static factories.
/// </summary>
public class BaseResponse : IBaseResponse
{
    protected BaseResponse(HttpStatusCode status, string? message, IReadOnlyDictionary<string, string[]>? errors)
    {
        Status = status;
        Message = message;
        Errors = errors;
    }

    /// <summary>Not serialized: the HTTP status line already carries it.</summary>
    [JsonIgnore]
    public HttpStatusCode Status { get; }

    [JsonPropertyOrder(-2)]
    public int StatusCode => (int)Status;

    [JsonPropertyOrder(-1)]
    public bool Success => StatusCode is >= 200 and < 300;

    public string? Message { get; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public string? TraceId { get; set; }

    public static BaseResponse Ok(string? message = null) => new(HttpStatusCode.OK, message, null);

    public static BaseResponse NoContent() => new(HttpStatusCode.NoContent, null, null);

    public static BaseResponse BadRequest(string message) => Fail(HttpStatusCode.BadRequest, message);

    public static BaseResponse ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        Fail(HttpStatusCode.BadRequest, ResponseMessages.ValidationFailed, errors);

    public static BaseResponse Unauthorized(string message) => Fail(HttpStatusCode.Unauthorized, message);

    public static BaseResponse Forbidden(string message) => Fail(HttpStatusCode.Forbidden, message);

    public static BaseResponse NotFound(string message) => Fail(HttpStatusCode.NotFound, message);

    public static BaseResponse Conflict(string message) => Fail(HttpStatusCode.Conflict, message);

    public static BaseResponse Error(string message) => Fail(HttpStatusCode.InternalServerError, message);

    public static BaseResponse Fail(HttpStatusCode status, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(status, message, errors);
}
