using SmartTask.Application.Common.Responses;

namespace SmartTask.Application.Common.Exceptions;

/// <summary>
/// An operation needs a user identity and there is none. The global exception handler maps it to 401
/// with <see cref="ErrorCodes.AuthenticationRequired"/>. Named to avoid clashing with
/// <see cref="System.UnauthorizedAccessException"/>, which means a file-system or OS access failure.
/// </summary>
public sealed class UnauthenticatedException : Exception
{
    public UnauthenticatedException()
        : base(ResponseMessages.AuthenticationRequired)
    {
    }

    public UnauthenticatedException(string message)
        : base(message)
    {
    }
}
