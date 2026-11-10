using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using SmartTask.Api.Http;
using SmartTask.Api.RateLimiting;
using SmartTask.Application.Common.Responses;
using SmartTask.Infrastructure.Identity;

namespace SmartTask.Api.Extensions;

public static class RateLimitingExtensions
{
    private const string AnonymousPartition = "anonymous";

    /// <summary>
    /// Registers the <c>global</c> limiter (fixed window per user, otherwise per IP; it applies to every
    /// endpoint that doesn't opt out with <c>DisableRateLimiting</c>), the <c>auth</c> policy (sliding
    /// window per IP) and the <c>ai</c> policy (token bucket per user). Rejections return a 429
    /// <see cref="BaseResponse"/> with <c>Retry-After</c>.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(UserOrIp(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.Global.PermitLimit,
                    Window = TimeSpan.FromSeconds(limits.Global.WindowSeconds),
                    QueueLimit = 0,
                }));

            options.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetSlidingWindowLimiter(Ip(context), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = limits.Auth.PermitLimit,
                    Window = TimeSpan.FromSeconds(limits.Auth.WindowSeconds),
                    SegmentsPerWindow = limits.Auth.SegmentsPerWindow,
                    QueueLimit = 0,
                }));

            options.AddPolicy(RateLimitPolicies.Ai, context =>
                RateLimitPartition.GetTokenBucketLimiter(UserOrIp(context), _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = limits.Ai.TokenLimit,
                    TokensPerPeriod = limits.Ai.TokensPerPeriod,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(limits.Ai.ReplenishmentSeconds),
                    AutoReplenishment = true,
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var fromLease)
                    ? fromLease
                    : EstimateRetryAfter(context.HttpContext, limits);

                context.HttpContext.Response.Headers[HeaderNames.RetryAfter] =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);

                await ApiResponseWriter.WriteAsync(
                    context.HttpContext,
                    BaseResponse.Fail(HttpStatusCode.TooManyRequests, "Too many requests. Please try again later."),
                    cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// The sliding-window limiter does not attach <c>RetryAfter</c> to a rejected lease, so this works it
    /// out from the configuration. A permit frees up at the latest when the current segment ends, so one
    /// segment length is an honest upper bound. Fixed-window rejections normally carry the exact value.
    /// </summary>
    private static TimeSpan EstimateRetryAfter(HttpContext context, RateLimitingOptions limits)
    {
        var policy = context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

        if (policy == RateLimitPolicies.Ai)
        {
            // One token comes back per (period / tokens-per-period).
            return TimeSpan.FromSeconds((double)limits.Ai.ReplenishmentSeconds / Math.Max(limits.Ai.TokensPerPeriod, 1));
        }

        var limit = policy == RateLimitPolicies.Auth ? limits.Auth : limits.Global;
        return TimeSpan.FromSeconds((double)limit.WindowSeconds / Math.Max(limit.SegmentsPerWindow, 1));
    }

    /// <summary>
    /// The user id once authentication has run (so <c>UseRateLimiter</c> must come after
    /// <c>UseAuthentication</c>), otherwise the IP address.
    /// </summary>
    private static string UserOrIp(HttpContext context) =>
        context.User.FindFirstValue(AppClaimTypes.Subject) is { Length: > 0 } userId ? $"user:{userId}" : Ip(context);

    private static string Ip(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address ? $"ip:{address}" : AnonymousPartition;
}
