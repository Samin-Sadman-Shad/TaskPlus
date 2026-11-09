namespace SmartTask.Domain.Constants;

public static class RefreshTokenConstraints
{
    /// <summary>Long enough for a full IPv6 address in text form.</summary>
    public const int CreatedByIpMaxLength = 45;
    public const int RevokedReasonMaxLength = 200;
}
