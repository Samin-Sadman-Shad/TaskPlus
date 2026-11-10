using System.Net.Http.Json;
using System.Text.Json;

namespace SmartTask.Api.IntegrationTests;

/// <summary>Helpers for reading the BaseResponse envelope as raw JSON, so tests check the wire format and not our C# types.</summary>
internal static class ApiJson
{
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace($"expected a JSON body for {(int)response.StatusCode}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>Asserts the common envelope fields and returns the parsed body.</summary>
    public static async Task<JsonElement> ShouldBeEnvelopeAsync(this HttpResponseMessage response, int expectedStatus)
    {
        ((int)response.StatusCode).ShouldBe(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var json = await ReadAsync(response);
        json.GetProperty("statusCode").GetInt32().ShouldBe(expectedStatus);
        json.GetProperty("success").GetBoolean().ShouldBe(expectedStatus is >= 200 and < 300);
        json.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        return json;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body);
}
