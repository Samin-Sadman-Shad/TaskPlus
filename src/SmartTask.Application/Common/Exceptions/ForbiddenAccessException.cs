using SmartTask.Application.Common.Responses;

namespace SmartTask.Application.Common.Exceptions;

/// <summary>
/// The caller is authenticated but may not perform the operation. The global exception handler maps
/// it to 403 with <see cref="ErrorCode"/>. Use it only where a <see cref="BaseResponse"/> can't be
/// returned; services return <see cref="BaseResponse.Forbidden"/> instead of throwing.
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message, string errorCode = ErrorCodes.ResourceForbidden)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
