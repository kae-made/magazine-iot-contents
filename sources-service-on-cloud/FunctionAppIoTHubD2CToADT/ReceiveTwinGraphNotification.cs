// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FunctionAppIoTHubD2CToADT
{
    public static class ReceiveTwinGraphNotification
    {
        [FunctionName("ReceiveTwinGraphNotification")]
        public static async Task Run([EventGridTrigger] EventGridEvent eventGridEvent,
            [SignalR(HubName = "ADTHub", ConnectionStringSetting = "AzureSignalRConnectionString")] IAsyncCollector<SignalRMessage> signalRMessage,
            ILogger log)
        {
            var exceptions = new List<Exception>();

            log.LogInformation($"Id = {eventGridEvent.Id}");
            log.LogInformation($"EventTime = {eventGridEvent.EventTime}");
            log.LogInformation($"EventType ={eventGridEvent.EventType}");
            log.LogInformation($"DataVersion = {eventGridEvent.DataVersion}");
            log.LogInformation($"Topic = {eventGridEvent.Topic}");
            log.LogInformation($"Subject={eventGridEvent.Subject}");
            log.LogInformation($"Data = {System.Text.Encoding.UTF8.GetString(eventGridEvent.Data)}");

            // Send notified message to SignalR
            try
            {
                var message = new
                {
                    EventType = eventGridEvent.EventType,
                    Data = System.Text.Encoding.UTF8.GetString(eventGridEvent.Data)
                };
                log.LogInformation("Sending message to SignalR");
                await signalRMessage.AddAsync(new SignalRMessage()
                {
                    Target = "TwinGraphUpdated",
                    Arguments = new[] { message }
                });
                log.LogInformation($"Succeeded to send message to SignalR");
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();

        }
    }
}
