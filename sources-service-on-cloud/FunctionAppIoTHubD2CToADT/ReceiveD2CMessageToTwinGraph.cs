using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace FunctionAppIoTHubD2CToADT
{
    public static class ReceiveD2CMessageToTwinGraph
    {
        static readonly string deviceIdKey = "iothub-connection-device-id";
        static readonly string modelIdKey = "dt-dataschema";

        static DigitalTwinsClient adtClient = null;
        static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("ReceiveD2CMessageToTwinGraph")]
        public static async Task Run([EventHubTrigger("iothub-mr-1", Connection = "iothubmsgrouting")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            if (adtClient == null)
            {
                var configuration = new ConfigurationBuilder().AddJsonFile("local.settings.json", true).AddEnvironmentVariables().Build();
                string adtUrl = configuration.GetConnectionString("ADT");
                // var credential = new ManagedIdentityCredential("https://digitaltwins.azure.net");
                var credential = new DefaultAzureCredential();
                adtClient = new DigitalTwinsClient(new Uri(adtUrl), credential,
                        new DigitalTwinsClientOptions
                        {
                            Transport = new HttpClientTransport(httpClient)
                        });
            }

            foreach (EventData eventData in events)
            {
                try
                {
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {eventData.EventBody}");
                    if (eventData.SystemProperties.Count > 0)
                    {
                        log.LogInformation("System Properties:");
                    }
                    foreach(var key in eventData.SystemProperties.Keys)
                    {
                        log.LogInformation($" - {key}:{eventData.SystemProperties[key]}");
                    }
                    if (eventData.Properties.Count > 0)
                    {
                        log.LogInformation("Application Properties:");
                    }
                    foreach(var key in eventData.Properties.Keys)
                    {
                        log.LogInformation($" - {key}:{eventData.Properties[key]}");
                    }

                    string deviceId = "";
                    if (eventData.SystemProperties.ContainsKey(deviceIdKey))
                    {
                        deviceId = (string)eventData.SystemProperties[deviceIdKey];
                    }
                    string modelId = "";
                    if (eventData.SystemProperties.ContainsKey(modelIdKey))
                    {
                        modelId = (string)eventData.SystemProperties[modelIdKey];
                    }
                    log.LogInformation($"deviceId={deviceId},modelId={modelId}");
                    if (!string.IsNullOrEmpty(deviceId) && !string.IsNullOrEmpty(modelId))
                    {
                        log.LogInformation("Try update!");
                        var dataContents = new Dictionary<string, object>();
                        var eventBody = Newtonsoft.Json.JsonConvert.DeserializeObject($"{eventData.EventBody}");
                        if (eventBody is JObject)
                        {
                            var eventBodyJObject = (JObject)eventBody;
                            foreach (var property in eventBodyJObject.Properties())
                            {
                                string propertyName = property.Name;
                                foreach (var child in property.Children())
                                {
                                    dataContents.Add(propertyName, GetJsonContent(child));
                                }
                            }
                        }                        
                        var updateTwins = new JsonPatchDocument();
                        foreach(var propKey in dataContents.Keys)
                        {
                            log.LogInformation($"Updating {propKey}");
                            updateTwins.AppendReplace($"/{propKey}", dataContents[propKey]);
                            try
                            {
                                await adtClient.UpdateDigitalTwinAsync(deviceId, updateTwins);
                                log.LogInformation("Replaced");
                            }
                            catch (Exception ex)
                            {
                                log.LogInformation($"{ex.Message}");
                                updateTwins = new JsonPatchDocument();
                                updateTwins.AppendAdd($"/{propKey}", dataContents[propKey]);
                                await adtClient.UpdateDigitalTwinAsync(deviceId, updateTwins);
                                log.LogInformation("Added");
                            }
                        }
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

        public static object GetJsonContent(JToken token)
        {
            object result = null;
            switch (token.Type)
            {
                case JTokenType.Object:
                    var jobject = (JObject)token;
                    var ownDataHolder = new Dictionary<string, object>();
                    foreach (var childProp in jobject.Children())
                    {
                        if (childProp.Type == JTokenType.Property)
                        {
                            var property = (JProperty)childProp;
                            ownDataHolder.Add(property.Name, GetJsonContent(property.Value));
                        }
                    }
                    result = ownDataHolder;
                    break;
                case JTokenType.Property:
                    throw new IndexOutOfRangeException("Property shouldn't exist!");
                    break;
                case JTokenType.Boolean:
                case JTokenType.Array:
                case JTokenType.Bytes:
                case JTokenType.Date:
                case JTokenType.Float:
                case JTokenType.Guid:
                case JTokenType.Integer:
                case JTokenType.String:
                case JTokenType.TimeSpan:
                case JTokenType.Uri:
                case JTokenType.None:
                case JTokenType.Null:
                case JTokenType.Raw:
                    result = ((JValue)token).Value;
                    break;
                case JTokenType.Undefined:
                    break;

            }
            return result;
        }

    }
}
