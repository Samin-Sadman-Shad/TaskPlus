using SmartTask.Application.DTOs.Users;
using SmartTask.Application.Validators.Common;

namespace SmartTask.Application.Validators.Users;

/// <summary>Rules for <c>PUT users/me</c>: the same name rules as registration.</summary>
public sealed class UpdateUserProfileDtoValidator : PersonNameValidator<UpdateUserProfileDto>;
