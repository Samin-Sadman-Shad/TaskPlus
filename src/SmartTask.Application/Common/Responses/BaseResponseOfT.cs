using System.Net;

namespace SmartTask.Application.Common.Responses;

/// <summary>Response with a payload. <see cref="Data"/> is <c>null</c> on failure.</summary>
/// <remarks>
/// The factories hide the non-generic ones (<c>new</c>), so <c>BaseResponse&lt;T&gt;.NotFound(...)</c>
/// returns the generic type a service method declares.
/// </remarks>
public sealed class BaseResponse<T> : BaseResponse
{
    private BaseResponse(HttpStatusCode status, T? data, string? message, IReadOnlyDictionary<string, string[]>? errors)
        : base(status, message, errors)
    {
        Data = data;
    }

    public T? Data { get; }

    public static BaseResponse<T> Ok(T data, string? message = null) => new(HttpStatusCode.OK, data, message, null);

    public static BaseResponse<T> Created(T data, string? message = null) => new(HttpStatusCode.Created, data, message, null);

    public static new BaseResponse<T> BadRequest(string message) => Fail(HttpStatusCode.BadRequest, message);

    public static new BaseResponse<T> ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        Fail(HttpStatusCode.BadRequest, ResponseMessages.ValidationFailed, errors);

    public static new BaseResponse<T> Unauthorized(string message) => Fail(HttpStatusCode.Unauthorized, message);

    public static new BaseResponse<T> Forbidden(string message) => Fail(HttpStatusCode.Forbidden, message);

    public static new BaseResponse<T> NotFound(string message) => Fail(HttpStatusCode.NotFound, message);

    public static new BaseResponse<T> Conflict(string message) => Fail(HttpStatusCode.Conflict, message);

    public static new BaseResponse<T> Error(string message) => Fail(HttpStatusCode.InternalServerError, message);

    public static new BaseResponse<T> Fail(HttpStatusCode status, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(status, default, message, errors);
}
