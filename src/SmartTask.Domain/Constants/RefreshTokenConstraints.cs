namespace SmartTask.Domain.Constants;

public static class RefreshTokenConstraints
{
    /// <summary>Room for a SHA-256 hash in hex (64) or Base64 (44) form, with headroom for a future algorithm.</summary>
    public const int TokenHashMaxLength = 128;

    /// <summary>Long enough for a full IPv6 address in text form.</summary>
    public const int CreatedByIpMaxLength = 45;

    public const int RevokedReasonMaxLength = 200;
}
