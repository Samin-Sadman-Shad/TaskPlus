using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartTask.Api.Authorization;
using SmartTask.Api.Controllers;
using SmartTask.Api.Middleware;
using SmartTask.Application.Common.Exceptions;
using SmartTask.Application.Common.Responses;
using SmartTask.Domain.Enums;

namespace SmartTask.Api.IntegrationTests;

/// <summary>Guards that keep role names, controller attributes and exception mapping consistent, without HTTP.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationMetadataTests(SmartTaskApiFactory factory)
{
    private static readonly Type[] SecuredControllers = typeof(BaseApiController).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(BaseApiController)) && t != typeof(AuthController))
        .ToArray();

    [Fact]
    public void AppRoles_MatchTheUserRoleEnum()
    {
        // Fails when a UserRole member is added without updating AppRoles.
        AppRoles.All.ShouldBe(Enum.GetNames<UserRole>(), ignoreOrder: true);
        AppRoles.Any.Split(',').ShouldBe(Enum.GetNames<UserRole>(), ignoreOrder: true);
        AppRoles.Management.Split(',').ShouldBe([nameof(UserRole.Admin), nameof(UserRole.ProjectManager)], ignoreOrder: true);
    }

    [Fact]
    public void EverySecuredController_HasAClassLevelRoleBaseline()
    {
        SecuredControllers.ShouldNotBeEmpty();

        foreach (var controller in SecuredControllers)
        {
            var attribute = controller.GetCustomAttribute<AuthorizeAttribute>();
            attribute.ShouldNotBeNull($"{controller.Name} has no class-level [Authorize]");
            attribute.Roles.ShouldBe(AppRoles.Any, $"{controller.Name} baseline");
            controller.GetCustomAttribute<AllowAnonymousAttribute>().ShouldBeNull($"{controller.Name} must not be anonymous");
        }
    }

    [Fact]
    public void NoEndpoint_UsesANamedPolicyOrAllowAnonymous()
    {
        foreach (var controller in SecuredControllers)
        {
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                action.GetCustomAttributes<AuthorizeAttribute>()
                    .ShouldAllBe(a => a.Policy == null, $"{controller.Name}.{action.Name} uses a named policy; use Roles");
                action.GetCustomAttribute<AllowAnonymousAttribute>().ShouldBeNull($"{controller.Name}.{action.Name} is anonymous");
            }
        }
    }

    [Theory]
    [InlineData(typeof(UsersController), nameof(UsersController.GetPaged), AppRoles.Admin)]
    [InlineData(typeof(UsersController), nameof(UsersController.Lookup), AppRoles.Management)]
    [InlineData(typeof(UsersController), nameof(UsersController.ChangeRole), AppRoles.Admin)]
    [InlineData(typeof(ProjectsController), nameof(ProjectsController.Create), AppRoles.Management)]
    [InlineData(typeof(TasksController), nameof(TasksController.Assign), AppRoles.Management)]
    [InlineData(typeof(TasksController), nameof(TasksController.UpdateStatus), null)]
    [InlineData(typeof(UsersController), nameof(UsersController.GetCurrent), null)]
    public void Action_NarrowsTheBaselineOnlyWhereTheMatrixSaysSo(Type controller, string action, string? expectedRoles)
    {
        var attribute = controller.GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();

        attribute?.Roles.ShouldBe(expectedRoles);
        if (expectedRoles is null)
        {
            attribute.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData(true, 403, "RESOURCE_FORBIDDEN")]
    [InlineData(false, 401, "AUTH_REQUIRED")]
    public async Task GlobalExceptionHandler_MapsAuthorizationExceptions(bool forbidden, int expectedStatus, string expectedCode)
    {
        var context = new DefaultHttpContext { RequestServices = factory.Services };
        context.Response.Body = new MemoryStream();
        Exception exception = forbidden ? new ForbiddenAccessException("Nope.") : new UnauthenticatedException();

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(expectedStatus);
        context.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe(expectedCode);
    }

    [Fact]
    public async Task ForbiddenAccessException_KeepsAnExplicitCode()
    {
        var context = new DefaultHttpContext { RequestServices = factory.Services };
        context.Response.Body = new MemoryStream();

        await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, new ForbiddenAccessException("Nope.", ErrorCodes.InsufficientRole), CancellationToken.None);

        context.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe(ErrorCodes.InsufficientRole);
    }
}
