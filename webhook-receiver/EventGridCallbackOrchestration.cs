using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Webhook.Receiver;
public class EventGridCallbackOrchestration(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    [Function(nameof(EventGridCallbackOrchestration))]
    public async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context, string WebHookRequestCallback)
    {
        var result = await context.CallActivityAsync<bool>(nameof(ValidateEventGridCallbackActivity), WebHookRequestCallback);
        if (!result)
        {
            throw new InvalidOperationException("Failed to validate callback");
        }

        Console.WriteLine("Callback validated successfully");
    }

    [Function(nameof(ValidateEventGridCallbackActivity))]
    public async Task<bool> ValidateEventGridCallbackActivity(
        [ActivityTrigger] string WebHookRequestCallback)
    {
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token;
        try
        {
            var WebHookRequestCallbackUrl = new Uri(WebHookRequestCallback);
            Console.WriteLine($"Validating callback: {WebHookRequestCallbackUrl}");

            await ValidateEventGridCallback(WebHookRequestCallbackUrl, 0, ct);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to validate callback: {e}");
            return false;
        }
    }

    private async Task ValidateEventGridCallback(Uri WebHookRequestCallbackUrl, int retryCount, CancellationToken ct)
    {
        var callbackResponse = await _httpClient.GetAsync(WebHookRequestCallbackUrl, ct);
        callbackResponse.EnsureSuccessStatusCode();
        var callbackResponseBody = await callbackResponse.Content.ReadAsStringAsync(ct);
        Console.WriteLine($"Callback response body: {callbackResponseBody}");
        if (callbackResponseBody == "\"Webhook successfully validated as a subscription endpoint.\"")
        {
            Console.WriteLine("Callback successfully validated");
            return;
        }

        if (retryCount < 5)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
            Console.WriteLine($"Retrying callback in {delay}...");
            await Task.Delay(delay, ct);
            await ValidateEventGridCallback(WebHookRequestCallbackUrl, retryCount + 1, ct);
            return;
        }

        throw new InvalidOperationException($"Failed to validate callback after {retryCount} retries");
    }
}
