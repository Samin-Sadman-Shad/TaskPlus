namespace SmartTask.Application.DTOs.Users;

/// <summary>
/// The name fields that <see cref="Auth.RegisterRequestDto"/> and <see cref="UpdateUserProfileDto"/>
/// share. It exists so one validator base class holds the name rules for both.
/// </summary>
public interface IPersonName
{
    string FirstName { get; }

    string LastName { get; }
}
