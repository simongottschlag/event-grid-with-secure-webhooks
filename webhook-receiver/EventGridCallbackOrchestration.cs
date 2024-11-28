using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Webhook.Receiver;
public class EventGridCallbackOrchestration(ILogger<EventGridCallbackOrchestration> logger, HttpClient httpClient)
{
    private readonly ILogger _logger = logger;
    private readonly HttpClient _httpClient = httpClient;
    [Function(nameof(EventGridCallbackOrchestration))]
    public async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context, string WebHookRequestCallback)
    {
        bool result;
        try
        {
            result = await context.CallActivityAsync<bool>(nameof(ValidateEventGridCallbackActivity), WebHookRequestCallback);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate callback");
            throw new ApplicationException("Failed to validate callback", ex);
        }

        if (!result)
        {
            throw new InvalidOperationException("Failed to validate callback, null result");
        }

        _logger.LogInformation("Callback validated successfully");
    }

    [Function(nameof(ValidateEventGridCallbackActivity))]
    public async Task ValidateEventGridCallbackActivity(
        [ActivityTrigger] string WebHookRequestCallback, CancellationToken ct)
    {
        Uri? webhookRequestCallbackUrl = null;
        try
        {
            webhookRequestCallbackUrl = new Uri(WebHookRequestCallback);
            if (webhookRequestCallbackUrl is null)
            {
                throw new InvalidOperationException("WebHook-Request-Callback header is not a valid URL, returned null");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebHook-Request-Callback header is not a valid URL");
            throw new InvalidOperationException("WebHook-Request-Callback header is not a valid URL", ex);
        }

        _logger.LogInformation($"Validating callback: {webhookRequestCallbackUrl}");


        try
        {
            var callbackSuccessful = await ValidateEventGridCallback(webhookRequestCallbackUrl, 0, ct);
            if (!callbackSuccessful)
            {
                throw new InvalidOperationException("Failed to validate callback, not successful");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to validate callback");
            throw new InvalidOperationException("Failed to validate callback", e);
        }
    }

    private async Task<bool> ValidateEventGridCallback(Uri WebHookRequestCallbackUrl, int retryCount, CancellationToken ct)
    {
        HttpResponseMessage? callbackResponse;
        try
        {
            callbackResponse = await _httpClient.GetAsync(WebHookRequestCallbackUrl, ct);
            if (!callbackResponse.IsSuccessStatusCode)
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate callback");
            return false;
        }

        string? callbackResponseBody;
        try
        {
            callbackResponseBody = await callbackResponse.Content.ReadAsStringAsync(ct);
            if (callbackResponseBody is null)
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read callback response body");
            return false;
        }

        _logger.LogInformation("Callback response body: {callbackResponseBody}", callbackResponseBody);


        if (callbackResponseBody == "\"Webhook successfully validated as a subscription endpoint.\"")
        {
            _logger.LogInformation("Callback validated successfully");
            return true;
        }

        if (retryCount < 5)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
            Console.WriteLine($"Retrying callback in {delay}...");
            await Task.Delay(delay, ct);

            try
            {
                var callbackResult = await ValidateEventGridCallback(WebHookRequestCallbackUrl, retryCount + 1, ct);
                if (!callbackResult)
                {
                    _logger.LogError("Failed to validate callback after {retryCount} retries", retryCount);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate callback");
                return false;
            }

            return true;
        }

        _logger.LogError("Failed to validate callback after {retryCount} retries", retryCount);
        return false;
    }
}
