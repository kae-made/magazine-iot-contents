import asyncio
import requests
import base64
from azure.storage.blob import BlobServiceClient
import datetime
from azure.iot.device import Message, MethodResponse
from azure.iot.device.aio import IoTHubModuleClient
import json
import time

get_duration_in_sec = 60    
blob_account_name = None
blob_account_key = None
blob_service_address = None
container_name = 'files'
rtsp_service_url = None
getimageloop_task = None
device_client = None
lock = None
waiting_flag = True

def stdin_listener():
    """
    Listener for quitting the sample
    """
    global waiting_flag
    global lock
    while waiting_flag:
        now = datetime.datetime.now()
        print("healthy - {}".format(now.strftime("%Y/%m/%d-%H:%M:%S")))
        time.sleep(60)

    print("loop exited.")
        
def stdin_listener_old():
    """
    Listener for quitting the sample
    """
    while True:
        selection = input("Press Q to quit\n")
        if selection == "Q" or selection == "q":
            print("Quitting...")
            break

async def get_image_loop(device_client, lock):
    global blob_account_name
    global blob_account_key
    global blob_service_address
    connection_string = f'DefaultEndpointsProtocol=http;BlobEndpoint=http://{blob_service_address}:11002/{blob_account_name};AccountName={blob_account_name};AccountKey={blob_account_key};'
    blobservice = BlobServiceClient.from_connection_string(conn_str=connection_string, api_version='2019-07-07')
    global container_name
    global rtsp_service_url
    global get_duration_in_sec

    try:
        print("settings:")
        print(f"connection_string - '{connection_string}")
        print(f"rest_service_url - {rtsp_service_url}")
        print(f"duration in sec - {get_duration_in_sec}")

        await update_reported_properties({"status":"running"})

        print(f'Starting for {rtsp_service_url}...')
        while True:
            response = requests.get(rtsp_service_url, headers={'Connection': 'close'})
            if 'image-server-format' in response.headers:
                imgbuf = base64.b64decode(response.content)
                imgfmt= response.headers['image-server-format']
                now = datetime.datetime.now()
                blob_name = "img-{}.{}".format(now.strftime("%Y%m%d%H%M%S"),imgfmt)
                blobclient = blobservice.get_blob_client(container=container_name, blob=blob_name)
                blobclient.upload_blob(imgbuf)
                print("Uploaded {}".format(blob_name))

                d2cContent = {'uploaded_blob':blob_name, 'timestamp':now.strftime("%Y/%m/%dT%H:%M:%S")}
                d2cMsg = Message(json.dumps(d2cContent))
                await device_client.send_message_to_output(d2cMsg, "output")
                print("Sent - {}".format(json.dumps(d2cContent)))

                current_duration = get_duration_in_sec
                async with lock:
                    current_duration = get_duration_in_sec
            else:
                print("failed to get images.")
                break

            response.close()
            await asyncio.sleep(current_duration)
    except asyncio.CancelledError:
        print("get image loop task has been canceled.")
    except Exception as e:
        print(f"error happens. {e}")
        await update_reported_properties({"status":"rest-or-blob-service-unavailable"})
    getimageloop_task = None

async def resolevDesiredTwins(desiredTwins, lock):
    global get_duration_in_sec
    global blob_service_address
    global blob_account_name
    global blob_account_key
    global rtsp_service_url

    if 'duration_in_sec' in desiredTwins:
        async with lock:
            get_duration_in_sec = int(desiredTwins['duration_in_sec'])
    if 'blob_service_address' in desiredTwins:
        blob_service_address = desiredTwins['blob_service_address']
    if 'blob_account_name' in desiredTwins:
        blob_account_name = desiredTwins['blob_account_name']
    if 'blob_account_key' in desiredTwins:
        blob_account_key = desiredTwins['blob_account_key']
    if 'rtsp_service_url' in desiredTwins:
        rtsp_service_url = desiredTwins['rtsp_service_url']
    if 'container_name' in desiredTwins:
        container_name = desiredTwins['container_name']

    if get_duration_in_sec is None or blob_service_address is None or blob_account_name is None or blob_account_key is None or rtsp_service_url is None:
        print('Bad desired properties. user shoud specify following properties')
        print('  - blob_service_address')
        print('  - blob_account_name')
        print('  - blob_account_key')
        print('  - container_name')
        print('  - rtsp_service_url')
        return False
    return True

async def updated_desired_properties(patch):
    global lock
    print('desired properties update requesting...')
    await resolevDesiredTwins(patch, lock)

async def invoked_direct_methods(methodRequest):
    global lock
    print('invoking direct method - {}'.format(methodRequest.name))
    if methodRequest.name == 'restart':
        global getimageloop_task
        if getimageloop_task is not None:
            print('Canceling old get loop...')
            getimageloop_task.cancel()
        getimageloop_task = asyncio.create_task(get_image_loop(lock))

async def update_reported_properties(patch):
    global device_client
    await device_client.patch_twin_reported_properties(patch)
    print(f"updated reported properties - {json.dumps(patch)}")

async def main():
    global device_client
    global lock

    print('initializing...')

    device_client = IoTHubModuleClient.create_from_edge_environment()
    await device_client.connect()
    moduleTwins = await device_client.get_twin()
    lock = asyncio.Lock()
    await resolevDesiredTwins(moduleTwins['desired'], lock)

    device_client.on_twin_desired_properties_patch_received =updated_desired_properties
    device_client.on_method_request_received = invoked_direct_methods

    global getimageloop_task
    getimageloop_task = asyncio.create_task(get_image_loop(device_client, lock))

    loop = asyncio.get_running_loop()
    user_finished = loop.run_in_executor(None, stdin_listener)
    # # Wait for user to indicate they are done listening for method calls
    await user_finished

    print('terminated.')

if __name__ == "__main__":
    asyncio.run(main())
