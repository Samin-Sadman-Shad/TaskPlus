namespace SmartTask.Application.DTOs.Auth;

/// <summary>Request body for <c>POST auth/login</c>.</summary>
public sealed record LoginRequestDto
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    /// <summary>Hides the password, so the DTO can be logged safely.</summary>
    public override string ToString() =>
        $"{nameof(LoginRequestDto)} {{ {nameof(Email)} = {Email}, {nameof(Password)} = {AuthRedaction.Mask} }}";
}
