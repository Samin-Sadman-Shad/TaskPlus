namespace SmartTask.Api.Authorization;

/// <summary>
/// Marks the few endpoints a user may call while their password is temporary (set by an admin): reading
/// their own profile and changing the password. Every other authenticated endpoint answers 403
/// <c>PASSWORD_CHANGE_REQUIRED</c> (see <see cref="ApiAuthorizationResultHandler"/>). Anonymous endpoints
/// (auth, health) are never affected.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AllowedWhilePasswordChangeRequiredAttribute : Attribute;
