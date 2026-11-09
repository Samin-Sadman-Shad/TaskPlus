namespace SmartTask.Domain.Exceptions;

/// <summary>Thrown when a domain invariant or business rule is broken. Mapped to HTTP 400.</summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
