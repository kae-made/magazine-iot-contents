// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;

namespace FunctionAppIoTHubD2CToADT
{
    public static class ReceiveTwinGraphNotification
    {
        [FunctionName("ReceiveTwinGraphNotification")]
        public static void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation($"Id = {eventGridEvent.Id}");
            log.LogInformation($"EventTime = {eventGridEvent.EventTime}");
            log.LogInformation($"EventType ={eventGridEvent.EventType}");
            log.LogInformation($"DataVersion = {eventGridEvent.DataVersion}");
            log.LogInformation($"Topic = {eventGridEvent.Topic}");
            log.LogInformation($"Subject={eventGridEvent.Subject}");
            log.LogInformation($"Data = {System.Text.Encoding.UTF8.GetString(eventGridEvent.Data)}");
        }
    }
}
