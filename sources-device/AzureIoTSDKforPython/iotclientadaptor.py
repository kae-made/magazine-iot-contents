import json

from azure.iot.device import Message, MethodResponse

class IoTClientAdaptor:
    def __init__(self, deviceClient):
        self.deviceClient = deviceClient

    async def sendTelemetry(self, message):
        msg = Message(json.dumps(message))
        msg.content_encoding = "utf-8"
        msg.content_type = "application/json"
        print("Sent message")
        await self.deviceClient.send_message(msg)

    async def updateReportedProperties(self, reportedProperties):
        await self.deviceClient.patch_twin_reported_properties(reportedProperties)
        log = "Updated reported properties by '{}'"
        print(log.format(json.dumps(reportedProperties)))

    async def getDesiredProperties(self):
        deviceTwins = await self.deviceClient.get_twin()
        return deviceTwins['desired']

    async def receiveTwinDesiredPropertiesPatch(self):
        return await self.deviceClient.receive_twin_desired_properties_patch()
    
    async def receiveMethodRequest(self, methodName):
        return await self.deviceClient.receive_method_request(methodName)
    
    async def returnMethodResponse(self, request, status, payload):
        command_response = MethodResponse.create_from_method_request(
            request, status, payload
        )

        try:
            await self.deviceClient.send_method_response(command_response)
        except Exception:
            print("responding to the {command} command failed".format(command=request.name))