using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Azure.Messaging;

namespace EventGrid.Publisher;

public class Publish
{
    private readonly ILogger<Publish> _logger;

    public Publish(ILogger<Publish> logger)
    {
        _logger = logger;
    }

    [Function("publish")]
    [EventGridOutput(Connection = "EVENT_GRID_TOPIC")]
    public CloudEvent Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var outputEvent = new CloudEvent("event-grid-publisher", "TestEvent", new { foo = "bar" });
        return outputEvent;
    }
}
