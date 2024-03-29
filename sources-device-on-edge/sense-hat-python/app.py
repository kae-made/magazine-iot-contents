# -------------------------------------------------------------------------
# Copyright (c) Knowledge & Experience. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import os
import asyncio
import random
import logging
import json

from azure.iot.device.aio import IoTHubModuleClient
from azure.iot.device.aio import ProvisioningDeviceClient
from azure.iot.device import Message, MethodResponse
from datetime import timedelta, datetime

from iotclientadaptor import IoTClientAdaptor
from iotapp import IoTApp

logging.basicConfig(level=logging.ERROR)

# The device "Thermostat" that is getting implemented using the above interfaces.
# This id can change according to the company the user is from
# and the name user wants to call this Plug and Play device
model_id = "dtmi:com:example:Thermostat;1"

#####################################################
# GLOBAL THERMOSTAT VARIABLES
max_temp = None
min_temp = None
avg_temp_list = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
moving_window_size = len(avg_temp_list)
target_temperature = None


#####################################################
# COMMAND HANDLERS : User will define these handlers
# depending on what commands the DTMI defines


async def reboot_handler(values):
    global max_temp
    global min_temp
    global avg_temp_list
    global target_temperature
    if values and type(values) == int:
        print("Rebooting after delay of {delay} secs".format(delay=values))
        asyncio.sleep(values)
    max_temp = None
    min_temp = None
    for idx in range(len(avg_temp_list)):
        avg_temp_list[idx] = 0
    target_temperature = None
    print("maxTemp {}, minTemp {}".format(max_temp, min_temp))
    print("Done rebooting")


async def max_min_handler(values):
    if values:
        print(
            "Will return the max, min and average temperature from the specified time {since} to the current time".format(
                since=values
            )
        )
    print("Done generating")


# END COMMAND HANDLERS
#####################################################

#####################################################
# CREATE RESPONSES TO COMMANDS


def create_max_min_report_response(values):
    """
    An example function that can create a response to the "getMaxMinReport" command request the way the user wants it.
    Most of the times response is created by a helper function which follows a generic pattern.
    This should be only used when the user wants to give a detailed response back to the Hub.
    :param values: The values that were received as part of the request.
    """
    response_dict = {
        "maxTemp": max_temp,
        "minTemp": min_temp,
        "avgTemp": sum(avg_temp_list) / moving_window_size,
        "startTime": (datetime.now() - timedelta(0, moving_window_size * 8)).isoformat(),
        "endTime": datetime.now().isoformat(),
    }
    # serialize response dictionary into a JSON formatted str
    response_payload = json.dumps(response_dict, default=lambda o: o.__dict__, sort_keys=True)
    print(response_payload)
    return response_payload


def create_reboot_response(values):
    response = {"result": True, "data": "reboot succeeded"}
    return response


# END CREATE RESPONSES TO COMMANDS
#####################################################

#####################################################
# TELEMETRY TASKS


async def send_telemetry_from_thermostat(device_client, telemetry_msg):
    msg = Message(json.dumps(telemetry_msg))
    msg.content_encoding = "utf-8"
    msg.content_type = "application/json"
    print("Sent message")
    await device_client.send_message(msg)


# END TELEMETRY TASKS
#####################################################

#####################################################
# CREATE COMMAND AND PROPERTY LISTENERS

async def direct_methods_listener(iotapp):
    await iotapp.directMethodsListener()

async def desired_properties_listener(iotapp):
    await iotapp.devicePropertiesListener()

async def execute_command_listener(
    device_client, method_name, user_command_handler, create_user_response_handler
):
    while True:
        if method_name:
            command_name = method_name
        else:
            command_name = None

        command_request = await device_client.receive_method_request(command_name)
        print("Command request received with payload")
        print(command_request.payload)

        values = {}
        if not command_request.payload:
            print("Payload was empty.")
        else:
            values = command_request.payload

        await user_command_handler(values)

        response_status = 200
        response_payload = create_user_response_handler(values)

        command_response = MethodResponse.create_from_method_request(
            command_request, response_status, response_payload
        )

        try:
            await device_client.send_method_response(command_response)
        except Exception:
            print("responding to the {command} command failed".format(command=method_name))


async def execute_property_listener(device_client):
    ignore_keys = ["__t", "$version"]
    while True:
        patch = await device_client.receive_twin_desired_properties_patch()  # blocking call

        print("the data in the desired properties patch was: {}".format(patch))

        version = patch["$version"]
        prop_dict = {}

        for prop_name, prop_value in patch.items():
            if prop_name in ignore_keys:
                continue
            else:
                prop_dict[prop_name] = {
                    "ac": 200,
                    "ad": "Successfully executed patch",
                    "av": version,
                    "value": prop_value,
                }

        await device_client.patch_twin_reported_properties(prop_dict)


# END COMMAND AND PROPERTY LISTENERS
#####################################################

#####################################################
# An # END KEYBOARD INPUT LISTENER to quit application


def stdin_listener():
    """
    Listener for quitting the sample
    """
    while True:
        selection = input("Press Q to quit\n")
        if selection == "Q" or selection == "q":
            print("Quitting...")
            break


# END KEYBOARD INPUT LISTENER
#####################################################


#####################################################
# PROVISION DEVICE
async def provision_device(provisioning_host, id_scope, registration_id, symmetric_key, model_id):
    provisioning_device_client = ProvisioningDeviceClient.create_from_symmetric_key(
        provisioning_host=provisioning_host,
        registration_id=registration_id,
        id_scope=id_scope,
        symmetric_key=symmetric_key,
    )
    provisioning_device_client.provisioning_payload = {"modelId": model_id}
    return await provisioning_device_client.register()


#####################################################
# MAIN STARTS
async def main():
    device_client = IoTHubModuleClient.create_from_edge_environment()

    # Connect the client.
    await device_client.connect()

    iotClientAdaptor = IoTClientAdaptor(device_client)
    iotapp = IoTApp(iotClientAdaptor)

    await iotapp.initlialize()

    ################################################
    # Set and read desired property (target temperature)
    # deviceTwins = await device_client.get_twin()
    # desiredTwins = deviceTwins['desired']

    # max_temp = 10.96  # Initial Max Temp otherwise will not pass certification
    # await device_client.patch_twin_reported_properties({"maxTempSinceLastReboot": max_temp})

    ################################################
    # Register callback and Handle command (reboot)
    print("Listening for command requests and property updates")

    #listeners = asyncio.gather(
    #    direct_methods_listener(iotapp),
    #    desired_properties_listener(iotapp)
        #execute_command_listener(
        #    device_client,
        #    method_name="reboot",
        #    user_command_handler=reboot_handler,
        #    create_user_response_handler=create_reboot_response,
        #),
        #execute_command_listener(
        #    device_client,
        #    method_name="getMaxMinReport",
        #    user_command_handler=max_min_handler,
        #    create_user_response_handler=create_max_min_report_response,
        #),
        #execute_property_listener(device_client),
    #)
    async def updated_desired_properties(patch):
        print("desired properties update requesting...")
        await iotapp.updatedDesiredProperties(patch)

    async def invoked_direct_methods(methodRequest):
        print("invoking direct method - {}".format(methodRequest.name))
        await iotapp.invokedDirectMethods(methodRequest)

    device_client.on_twin_desired_properties_patch_received = updated_desired_properties
    device_client.on_method_request_received = invoked_direct_methods

    ################################################
    # Send telemetry (current temperature)

    async def send_telemetry():
        print("Sending telemetry for temperature")
        global max_temp
        global min_temp
        current_avg_idx = 0

        while True:
            current_temp = random.randrange(10, 50)  # Current temperature in Celsius
            if not max_temp:
                max_temp = current_temp
            elif current_temp > max_temp:
                max_temp = current_temp

            if not min_temp:
                min_temp = current_temp
            elif current_temp < min_temp:
                min_temp = current_temp

            avg_temp_list[current_avg_idx] = current_temp
            current_avg_idx = (current_avg_idx + 1) % moving_window_size

            temperature_msg1 = {"temperature": current_temp}
            await send_telemetry_from_thermostat(device_client, temperature_msg1)
            await asyncio.sleep(8)

    async def iotapp_working(iotapp):
        print("Starting IoT app routine...")
        await iotapp.doWork()

    iotapp_dowork_task = asyncio.create_task(iotapp_working(iotapp))

    # Run the stdin listener in the event loop
    loop = asyncio.get_running_loop()
    user_finished = loop.run_in_executor(None, stdin_listener)
    # # Wait for user to indicate they are done listening for method calls
    await user_finished

    #if not listeners.done():
    #    listeners.set_result("DONE")

    #listeners.cancel()

    iotapp_dowork_task.cancel()

    # Finally, shut down the client
    await device_client.shutdown()


#####################################################
# EXECUTE MAIN

if __name__ == "__main__":
    asyncio.run(main())

    # If using Python 3.6 use the following code instead of asyncio.run(main()):
    # loop = asyncio.get_event_loop()
    # loop.run_until_complete(main())
    # loop.close()
