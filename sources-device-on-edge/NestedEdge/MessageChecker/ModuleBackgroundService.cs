using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System.Text;

namespace MessageChecker;

internal class ModuleBackgroundService : BackgroundService
{
    private int _counter;
    private ModuleClient? _moduleClient;
    private CancellationToken _cancellationToken;
    private readonly ILogger<ModuleBackgroundService> _logger;

    private static readonly string msgInputPortName = "inputMessage";
    private static readonly string msgOutputPortName = "outputMessage";

    public ModuleBackgroundService(ILogger<ModuleBackgroundService> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        MqttTransportSettings mqttSetting = new(TransportType.Mqtt_Tcp_Only);
        ITransportSettings[] settings = { mqttSetting };

        // Open a connection to the Edge runtime
        _moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

        // Reconnect is not implented because we'll let docker restart the process when the connection is lost
        _moduleClient.SetConnectionStatusChangesHandler((status, reason) => 
            _logger.LogWarning("Connection changed: Status: {status} Reason: {reason}", status, reason));

        await _moduleClient.OpenAsync(cancellationToken);

        _logger.LogInformation("IoT Hub module client initialized.");

        // Register callback to be called when a message is received by the module
        await _moduleClient.SetInputMessageHandlerAsync(msgInputPortName, ProcessMessageAsync, null, cancellationToken);
    }

    async Task<MessageResponse> ProcessMessageAsync(Message message, object userContext)
    {
        int counterValue = Interlocked.Increment(ref _counter);

        byte[] messageBytes = message.GetBytes();
        string messageString = Encoding.UTF8.GetString(messageBytes);
        string nowTime = DateTime.Now.ToString("yyMMddHHmmss.fff");
        _logger.LogInformation("From {msgInputPortName}", msgInputPortName);
        _logger.LogInformation("Received message@[{nowTime}]: {counterValue}, Body: [{messageString}]", nowTime, counterValue, messageString);
        if (message.Properties.Count > 0)
        {
            _logger.LogInformation("Properties:");
            foreach (var prop in message.Properties)
            {
                _logger.LogInformation($"  {prop.Key}:{prop.Value}");
            }
        }
        if (!string.IsNullOrEmpty(message.ComponentName))
        {
            _logger.LogInformation($" ComponentName:{message.ComponentName}");
        }
        if (!string.IsNullOrEmpty(message.ConnectionDeviceId))
        {
            _logger.LogInformation($" ConnectionDeviceId:{message.ConnectionDeviceId}");
        }
        if (!string.IsNullOrEmpty(message.ConnectionModuleId))
        {
            _logger.LogInformation($" ConnectionModuleId:{message.ConnectionModuleId}");
        }
        if (!string.IsNullOrEmpty(message.ContentEncoding))
        {
            _logger.LogInformation($" ContentEncoding:{message.ContentEncoding}");
        }
        if (!string.IsNullOrEmpty(message.ContentType))
        {
            _logger.LogInformation($" ContentType:{message.ContentType}");
        }
        if (!string.IsNullOrEmpty(message.CorrelationId))
        {
            _logger.LogInformation($" CorrelationId:{message.CorrelationId}");
        }
        _logger.LogInformation($" CreationTimeUtc:{message.CreationTimeUtc}");
        _logger.LogInformation($" EnqueuedTimeUtc:{message.EnqueuedTimeUtc}");
        _logger.LogInformation($" ExpiryTimeUtc:{message.ExpiryTimeUtc}");
        _logger.LogInformation($" DeliveryCount:{message.DeliveryCount}");
        if (!string.IsNullOrEmpty(message.InputName))
        {
            _logger.LogInformation($" InputName:{message.InputName}");
        }
        _logger.LogInformation($" IsSecurityMessage:{message.IsSecurityMessage}");
        if (!string.IsNullOrEmpty(message.MessageId))
        {
            _logger.LogInformation($" MessageId:{message.MessageId}");
        }
        if (!string.IsNullOrEmpty(message.MessageSchema))
        {
            _logger.LogInformation($" MessageSchema:{message.MessageSchema}");
        }
        _logger.LogInformation($" SequenceNumber:{message.SequenceNumber}");
        if (!string.IsNullOrEmpty(message.To))
        {
            _logger.LogInformation($" To:{message.To}");
        }
        if (!string.IsNullOrEmpty(message.UserId))
        {
            _logger.LogInformation($" UserId:{message.UserId}");
        }
        if (!string.IsNullOrEmpty(messageString))
        {
            using var pipeMessage = new Message(messageBytes);
            foreach (var prop in message.Properties)
            {
                pipeMessage.Properties.Add(prop.Key, prop.Value);
            }
            if (!string.IsNullOrEmpty(message.ConnectionDeviceId) && !pipeMessage.Properties.ContainsKey("SourceDeviceId"))
            {
                pipeMessage.Properties.Add("SourceDeviceId", message.ConnectionDeviceId);
                pipeMessage.Properties.Add("SourceModuleId", message.ConnectionModuleId);
                _logger.LogInformation($".. marked source as {message.ConnectionDeviceId}:{message.ConnectionModuleId}");
            }
            await _moduleClient!.SendEventAsync(msgOutputPortName, pipeMessage, _cancellationToken);

            _logger.LogInformation("Received message sent to {msgOutputPortName}", msgOutputPortName);
        }
        return MessageResponse.Completed;
    }
}
