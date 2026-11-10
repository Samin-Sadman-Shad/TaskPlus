using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartTask.Api.Controllers;
using SmartTask.Application.Common.Responses;

namespace SmartTask.Api.IntegrationTests.Controllers;

/// <summary>Unit tests for the single status → HTTP result mapping that every controller relies on.</summary>
public sealed class BaseApiControllerTests
{
    private const string TraceId = "trace-123";

    private readonly StubController _controller = new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = TraceId } },
    };

    public static TheoryData<HttpStatusCode, Type> MappedStatuses => new()
    {
        { HttpStatusCode.BadRequest, typeof(BadRequestObjectResult) },
        { HttpStatusCode.Unauthorized, typeof(UnauthorizedObjectResult) },
        { HttpStatusCode.Forbidden, typeof(ObjectResult) },
        { HttpStatusCode.NotFound, typeof(NotFoundObjectResult) },
        { HttpStatusCode.Conflict, typeof(ConflictObjectResult) },
        { HttpStatusCode.InternalServerError, typeof(ObjectResult) },
        { HttpStatusCode.BadGateway, typeof(ObjectResult) },
        { HttpStatusCode.ServiceUnavailable, typeof(ObjectResult) },
    };

    [Theory]
    [MemberData(nameof(MappedStatuses))]
    public void ToActionResult_FailureStatus_MapsToMatchingResultWithBody(HttpStatusCode status, Type expectedType)
    {
        var response = BaseResponse.Fail(status, "failure");

        var result = _controller.Map(response).Result.ShouldBeAssignableTo<ObjectResult>()!;

        result.GetType().ShouldBe(expectedType);
        result.StatusCode.ShouldBe((int)status);
        result.Value.ShouldBeSameAs(response);
        response.TraceId.ShouldBe(TraceId);
    }

    [Fact]
    public void ToActionResult_Ok_Returns200WithBody()
    {
        var response = BaseResponse<string>.Ok("data");

        var result = _controller.Map(response).Result.ShouldBeOfType<OkObjectResult>();

        result.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.Value.ShouldBeSameAs(response);
    }

    [Fact]
    public void ToActionResult_Created_Returns201WithBody() =>
        _controller.Map(BaseResponse<string>.Created("data")).Result
            .ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status201Created);

    [Fact]
    public void ToActionResult_NoContent_Returns204WithoutBody() =>
        // NoContentResult has no Value at all: writing a body on a 204 is invalid HTTP.
        _controller.Map(BaseResponse.NoContent()).Result.ShouldBeOfType<NoContentResult>();

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData((HttpStatusCode)418)]
    public void ToActionResult_UnknownStatus_PassesThroughUnchanged(HttpStatusCode status)
    {
        var result = _controller.Map(BaseResponse.Fail(status, "x")).Result.ShouldBeOfType<ObjectResult>();

        result.StatusCode.ShouldBe((int)status);
    }

    [Fact]
    public void ToActionResult_ExistingTraceId_IsKept()
    {
        var response = BaseResponse.NotFound("missing");
        response.TraceId = "upstream";

        _controller.Map(response);

        response.TraceId.ShouldBe("upstream");
    }

    [Fact]
    public void ToCreatedResult_Created_ReturnsCreatedAtActionWithRouteValues()
    {
        var id = Guid.NewGuid();

        var result = _controller.MapCreated(BaseResponse<Item>.Created(new Item(id))).Result.ShouldBeOfType<CreatedAtActionResult>();

        result.ActionName.ShouldBe("GetById");
        result.RouteValues!["id"].ShouldBe(id);
        result.StatusCode.ShouldBe(StatusCodes.Status201Created);
    }

    [Fact]
    public void ToCreatedResult_Failure_FallsBackToStatusMapping() =>
        _controller.MapCreated(BaseResponse<Item>.Conflict("taken")).Result.ShouldBeOfType<ConflictObjectResult>();

    public sealed record Item(Guid Id);

    private sealed class StubController : BaseApiController
    {
        public ActionResult<TResponse> Map<TResponse>(TResponse response)
            where TResponse : IBaseResponse => ToActionResult(response);

        public ActionResult<BaseResponse<Item>> MapCreated(BaseResponse<Item> response) =>
            ToCreatedResult(response, "GetById", item => new { id = item.Id });
    }
}
