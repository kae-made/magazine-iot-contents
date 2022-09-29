using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace FunctionAppForStreamAnalytics
{
    public static class ReceiveJoinedMessage
    {
        [FunctionName("ReceiveJoinedMessage")]
        public static async Task Run([EventHubTrigger("device-message", Connection = "eventhubcs_output")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger ReceiveJoinedMessage processed a message: {eventData.EventBody}");
                    string props = GetProperties(eventData);
                    if (!string.IsNullOrEmpty(props))
                    {
                        log.LogInformation(props);
                    }
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }

        private static string GetProperties(EventData eventData)
        {
            string props = "";
            if (eventData.SystemProperties.Count > 0)
            {
                props = "  -sp:";
                string propVals = "";
                foreach (var prop in eventData.SystemProperties)
                {
                    if (!string.IsNullOrEmpty(propVals))
                    {
                        propVals += ";";
                    }
                    propVals += $"{prop.Key}={prop.Value}";
                }
                props += propVals;
            }
            if (eventData.Properties.Count > 0)
            {
                if (string.IsNullOrEmpty(props))
                {
                    props = "  ";
                }
                props += "-ap:";
                string propVals = "";
                foreach (var prop in eventData.Properties)
                {
                    if (!string.IsNullOrEmpty(propVals))
                    {
                        propVals += ";";
                    }
                    propVals += $"{prop.Key}={prop.Value}";
                }
                props += propVals;
            }

            return props;
        }
    }
}
