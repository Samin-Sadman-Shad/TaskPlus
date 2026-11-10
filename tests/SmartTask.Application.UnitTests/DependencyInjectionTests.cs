using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.Application.Common.Models;
using SmartTask.Application.DTOs.Ai;
using SmartTask.Application.DTOs.Auth;
using SmartTask.Application.DTOs.Projects;
using SmartTask.Application.DTOs.Tasks;
using SmartTask.Application.DTOs.Users;

namespace SmartTask.Application.UnitTests;

public sealed class DependencyInjectionTests
{
    /// <summary>Every inbound request type. A new request DTO without a validator fails this test.</summary>
    private static readonly Type[] AllRequestTypes =
    [
        typeof(RegisterRequestDto),
        typeof(LoginRequestDto),
        typeof(UpdateUserProfileDto),
        typeof(ChangeUserRoleDto),
        typeof(CreateProjectDto),
        typeof(UpdateProjectDto),
        typeof(AddProjectMemberDto),
        typeof(CreateTaskDto),
        typeof(UpdateTaskDto),
        typeof(UpdateTaskStatusDto),
        typeof(AssignTaskDto),
        typeof(ImproveDescriptionRequestDto),
        typeof(ProjectQueryParameters),
        typeof(TaskQueryParameters),
        typeof(UserQueryParameters),
    ];

    public static TheoryData<Type> RequestTypes => new(AllRequestTypes);

    [Theory]
    [MemberData(nameof(RequestTypes))]
    public void AddApplication_RegistersExactlyOneValidatorPerRequestType(Type requestType)
    {
        using var provider = new ServiceCollection().AddApplication().BuildServiceProvider();
        using var scope = provider.CreateScope();

        var validators = scope.ServiceProvider.GetServices(typeof(IValidator<>).MakeGenericType(requestType)).ToList();

        validators.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddApplication_EveryRequestDtoInAssemblyIsCovered()
    {
        var requestDtos = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                && t.Namespace?.StartsWith("SmartTask.Application.DTOs", StringComparison.Ordinal) == true
                && (t.Name.StartsWith("Create", StringComparison.Ordinal)
                    || t.Name.StartsWith("Update", StringComparison.Ordinal)
                    || t.Name.StartsWith("Assign", StringComparison.Ordinal)
                    || t.Name.StartsWith("Add", StringComparison.Ordinal)
                    || t.Name.StartsWith("Change", StringComparison.Ordinal)
                    || t.Name.EndsWith("RequestDto", StringComparison.Ordinal)))
            .ToList();

        requestDtos.ShouldNotBeEmpty();
        requestDtos.ShouldAllBe(t => AllRequestTypes.Contains(t));
    }
}
