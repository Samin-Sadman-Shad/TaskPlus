using System.Text.Json;
using SmartTask.Application.Common.Responses;

namespace SmartTask.Application.UnitTests.Common;

public sealed class BaseResponseErrorCodeTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static JsonElement Serialize(object response) =>
        JsonDocument.Parse(JsonSerializer.Serialize(response, response.GetType(), Json)).RootElement;

    [Fact]
    public void Forbidden_DefaultsToResourceForbidden()
    {
        BaseResponse.Forbidden("x").ErrorCode.ShouldBe(ErrorCodes.ResourceForbidden);
        BaseResponse<int>.Forbidden("x").ErrorCode.ShouldBe(ErrorCodes.ResourceForbidden);

        Serialize(BaseResponse.Forbidden("x")).GetProperty("errorCode").GetString().ShouldBe("RESOURCE_FORBIDDEN");
    }

    [Fact]
    public void Forbidden_UsesExplicitCode()
    {
        BaseResponse<int>.Forbidden("x", ErrorCodes.InsufficientRole).ErrorCode.ShouldBe(ErrorCodes.InsufficientRole);
    }

    [Fact]
    public void Unauthorized_HasNoDefaultCode_ButKeepsAnExplicitOne()
    {
        // Services also use 401 for a wrong password, which is not "sign in first".
        BaseResponse.Unauthorized(ResponseMessages.InvalidCredentials).ErrorCode.ShouldBeNull();
        BaseResponse<int>.Unauthorized("x", ErrorCodes.TokenExpired).ErrorCode.ShouldBe(ErrorCodes.TokenExpired);
    }

    [Fact]
    public void Success_OmitsErrorCodeFromJson()
    {
        Serialize(BaseResponse.Ok()).TryGetProperty("errorCode", out _).ShouldBeFalse();
        Serialize(BaseResponse<int>.Ok(1)).TryGetProperty("errorCode", out _).ShouldBeFalse();
    }

    [Fact]
    public void ErrorCode_IsSerializedRightAfterMessage()
    {
        var names = Serialize(BaseResponse.Forbidden("x")).EnumerateObject().Select(p => p.Name).ToList();

        names.IndexOf("errorCode").ShouldBe(names.IndexOf("message") + 1);
    }
}
