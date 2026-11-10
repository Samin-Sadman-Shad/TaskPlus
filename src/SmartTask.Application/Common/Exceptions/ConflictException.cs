namespace SmartTask.Application.Common.Exceptions;

/// <summary>
/// Raised by the unit of work when a save conflicts with data written by someone else: an
/// optimistic-concurrency (<c>RowVersion</c>) mismatch or a unique-index violation.
/// Services convert it to HTTP 409.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
