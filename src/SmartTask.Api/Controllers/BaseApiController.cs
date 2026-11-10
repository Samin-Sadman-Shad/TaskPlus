using System.Net;
using Microsoft.AspNetCore.Mvc;
using SmartTask.Application.Common.Responses;

namespace SmartTask.Api.Controllers;

/// <summary>
/// Base for every controller. Actions call one service method and pass the <see cref="BaseResponse"/>
/// to <see cref="ToActionResult{TResponse}"/>, which maps its status to the HTTP result in one place
/// (template method). See API-CONVENTIONS §3.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<TResponse> ToActionResult<TResponse>(TResponse response)
        where TResponse : IBaseResponse
    {
        ArgumentNullException.ThrowIfNull(response);
        response.TraceId ??= HttpContext.TraceIdentifier;

        return response.Status switch
        {
            HttpStatusCode.OK => Ok(response),
            HttpStatusCode.Created => StatusCode((int)HttpStatusCode.Created, response),
            HttpStatusCode.NoContent => NoContent(), // a 204 must not carry a body
            HttpStatusCode.BadRequest => BadRequest(response),
            HttpStatusCode.Unauthorized => Unauthorized(response),
            HttpStatusCode.Forbidden => StatusCode((int)HttpStatusCode.Forbidden, response),
            HttpStatusCode.NotFound => NotFound(response),
            HttpStatusCode.Conflict => Conflict(response),
            HttpStatusCode.InternalServerError => StatusCode((int)HttpStatusCode.InternalServerError, response),
            HttpStatusCode.BadGateway => StatusCode((int)HttpStatusCode.BadGateway, response),
            HttpStatusCode.ServiceUnavailable => StatusCode((int)HttpStatusCode.ServiceUnavailable, response),
            _ => StatusCode((int)response.Status, response), // never remap an unknown code
        };
    }

    /// <summary>201 with a Location header pointing at the new resource; any other status falls back to <see cref="ToActionResult{TResponse}"/>.</summary>
    protected ActionResult<BaseResponse<T>> ToCreatedResult<T>(
        BaseResponse<T> response,
        string actionName,
        Func<T, object> routeValues)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(routeValues);

        if (response.Status != HttpStatusCode.Created || response.Data is null)
        {
            return ToActionResult(response);
        }

        response.TraceId ??= HttpContext.TraceIdentifier;
        return CreatedAtAction(actionName, routeValues(response.Data), response);
    }
}
