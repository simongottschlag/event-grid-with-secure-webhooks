using DarkLoop.Azure.Functions.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using static Utils.Invoke;

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
        var webhookRequestOriginResult = TryInvokeNotNull(() => GetHeaderValue(req, "WebHook-Request-Origin"));
        if (!webhookRequestOriginResult.IsSuccessful)
        {
            _logger.LogError(webhookRequestOriginResult.Error, "WebHook-Request-Origin header not found");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var webhookRequestOrigin = webhookRequestOriginResult.Value;
        if (webhookRequestOrigin != "eventgrid.azure.net")
        {
            _logger.LogError("WebHook-Request-Origin header is not a valid URL: {WebHookRequestOrigin}", webhookRequestOrigin);
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var validateEventGridWebhookResult = await TryInvoke(() => ValidateEventGridWebhook(req, durableTaskClient, webhookRequestOrigin, ct));
        if (!validateEventGridWebhookResult.IsSuccessful)
        {
            _logger.LogError(validateEventGridWebhookResult.Error, "Failed to validate event grid webhook");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        return validateEventGridWebhookResult.Value;
    }

    private async Task<HttpResponseData> ValidateEventGridWebhook(HttpRequestData req, DurableTaskClient durableTaskClient, string webhookRequestOrigin, CancellationToken ct)
    {

        var webhookRequestCallbackResult = TryInvokeNotNull(() => GetHeaderValue(req, "WebHook-Request-Callback"));
        if (!webhookRequestCallbackResult.IsSuccessful)
        {
            _logger.LogError(webhookRequestCallbackResult.Error, "WebHook-Request-Callback header not found");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var webhookRequestCallback = webhookRequestCallbackResult.Value;
        var webHookRequestCallbackUrlResult = TryInvokeNotNull(() => new Uri(webhookRequestCallback));
        if (!webHookRequestCallbackUrlResult.IsSuccessful)
        {
            _logger.LogError(webHookRequestCallbackUrlResult.Error, "WebHook-Request-Callback header is not a valid URL: {WebHookRequestCallback}", webhookRequestCallback);
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var webHookRequestCallbackUrl = webHookRequestCallbackUrlResult.Value;
        if (!webHookRequestCallbackUrl.DnsSafeHost.EndsWith("eventgrid.azure.net"))
        {
            _logger.LogError("WebHook-Request-Callback header is not a valid URL");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var durableTaskInstanceIdResult = await TryInvoke(() => durableTaskClient.ScheduleNewOrchestrationInstanceAsync(nameof(EventGridCallbackOrchestration), webhookRequestCallback, ct));
        if (!durableTaskInstanceIdResult.IsSuccessful)
        {
            _logger.LogError(durableTaskInstanceIdResult.Error, "Failed to start orchestration");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        var durableTaskInstanceId = durableTaskInstanceIdResult.Value;
        var checkStatusResponseResult = await TryInvokeNotNull(() => durableTaskClient.CreateCheckStatusResponseAsync(req, durableTaskInstanceId));
        if (!checkStatusResponseResult.IsSuccessful)
        {
            _logger.LogError(checkStatusResponseResult.Error, "Failed to create check status response");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        _logger.LogInformation("Started orchestration with ID = '{DurableTaskInstanceId}'", durableTaskInstanceId);

        return checkStatusResponseResult.Value;
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
