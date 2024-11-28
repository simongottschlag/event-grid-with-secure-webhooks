using DarkLoop.Azure.Functions.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;

namespace Webhook.Receiver;

[FunctionAuthorize]
public class Webhook(ILogger<Webhook> logger, HttpClient httpClient)
{
    private readonly ILogger<Webhook> _logger = logger;
    private readonly HttpClient _httpClient = httpClient;

    [Function("webhook")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "options", "post")] HttpRequestData req, [DurableClient] DurableTaskClient durableTaskClient, CancellationToken ct)
    {
        await PrintRequestDetails(req);

        return req.Method switch
        {
            "OPTIONS" => await RunOptions(req, durableTaskClient, ct),
            "POST" => await RunPost(req, ct),
            _ => req.CreateResponse(HttpStatusCode.MethodNotAllowed),
        };
    }

    private async Task<HttpResponseData> RunOptions(HttpRequestData req, DurableTaskClient durableTaskClient, CancellationToken ct)
    {
        string? WebHookRequestOrigin;
        try
        {
            WebHookRequestOrigin = GetHeaderValue(req, "WebHook-Request-Origin");
            if (string.IsNullOrEmpty(WebHookRequestOrigin))
            {
                throw new InvalidOperationException("WebHook-Request-Origin header is empty");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebHook-Request-Origin header not found");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        return WebHookRequestOrigin switch
        {
            "eventgrid.azure.net" => await ValidateEventGridWebhook(req, durableTaskClient, WebHookRequestOrigin, ct),
            _ => req.CreateResponse(HttpStatusCode.BadRequest),
        };
    }

    private async Task<HttpResponseData> ValidateEventGridWebhook(HttpRequestData req, DurableTaskClient durableTaskClient, string WebHookRequestOrigin, CancellationToken ct)
    {
        string? webHookRequestCallback = null;
        try
        {
            webHookRequestCallback = GetHeaderValue(req, "WebHook-Request-Callback");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebHook-Request-Callback header not found");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        Uri? webHookRequestCallbackUrl = null;
        try
        {
            webHookRequestCallbackUrl = new Uri(webHookRequestCallback);
            if (webHookRequestCallbackUrl is null)
            {
                throw new InvalidOperationException("WebHook-Request-Callback header is not a valid URL, returned null");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebHook-Request-Callback header is not a valid URL");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (!webHookRequestCallbackUrl.DnsSafeHost.EndsWith("eventgrid.azure.net"))
        {
            _logger.LogError("WebHook-Request-Callback header is not a valid URL");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        string durableTaskInstanceId;
        try
        {
            durableTaskInstanceId = await durableTaskClient.ScheduleNewOrchestrationInstanceAsync(nameof(EventGridCallbackOrchestration), webHookRequestCallback, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start orchestration");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        HttpResponseData? res = null;
        try
        {
            res = await durableTaskClient.CreateCheckStatusResponseAsync(req, durableTaskInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create check status response");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        if (res is null)
        {
            _logger.LogError("Failed to create check status response");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        Console.WriteLine($"Started orchestration with ID = '{durableTaskInstanceId}'.");

        return res;
    }

    private static async Task<HttpResponseData> RunPost(HttpRequestData req, CancellationToken ct)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.Body.WriteAsync(Encoding.UTF8.GetBytes("Hello, World!"), ct);
        return res;
    }

    private static async Task PrintRequestDetails(HttpRequestData req)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Request details:");
        sb.AppendLine($"  - Method: {req.Method}");
        sb.AppendLine($"  - URL: {req.Url}");
        sb.AppendLine($"  - Query: {req.Url.Query}");
        sb.AppendLine($"  - Path: {req.Url.AbsolutePath}");
        sb.AppendLine($"  - Host: {req.Url.Host}");
        sb.AppendLine($"  - Port: {req.Url.Port}");
        sb.AppendLine($"  - Scheme: {req.Url.Scheme}");
        sb.AppendLine($"  - Headers:");
        foreach (var header in req.Headers)
        {
            foreach (var value in header.Value)
            {
                sb.AppendLine($"    - {header.Key}: {value}");
            }
        }

        var body = await req.ReadAsStringAsync();
        sb.AppendLine($"  - Body:\n------\n{body}\n------");

        Console.WriteLine(sb);
    }

    private static string GetHeaderValue(HttpRequestData req, string headerName)
    {
        var values = req.Headers.GetValues(headerName) ?? throw new InvalidOperationException($"Header {headerName} not found");
        var value = values.FirstOrDefault();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Header {headerName} is empty");
        }

        return value;
    }
}
