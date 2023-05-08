from flask import Flask, jsonify, abort, make_response
import cv2
from PIL import Image
import io
from base64 import b64encode
import time
import threading
import os

number = 0
currentImage = None 
currentStatus = False

def capture(rtspUrl, lock):
    global currentImage
    global currentStatus
    cap = cv2.VideoCapture(rtspUrl)
    fps = 0.0
    currentStatus = cap.isOpened()
    if currentStatus:
        fps = float(cap.get(cv2.CAP_PROP_FPS))
        print(f'RTSP stream - connected - {rtspUrl}, fps={fps}')
    else:
        print(f'RTSP stream - has not connected - {rtspUrl}')
    
    while currentStatus:
        with lock:
            currentStatus = cap.isOpened()
            currentStatus, currentImage = cap.read()
        time.sleep(1.0/fps)
    print('RTSP stream has been closed.')
    print('please restart this module when you want to use.')

def start_server(restServerPort, lock):
    global currentImage
    global currentStatus
    api = Flask(__name__)
    @api.route('/image', methods=['GET'])
    def get():
        with lock:
            if currentStatus:
                frame = cv2.cvtColor(currentImage, cv2.COLOR_BGR2RGB)
                image = Image.fromarray(frame)
                bmp = io.BytesIO()
                image.save(bmp, format='bmp')
                encoded = b64encode(bmp.getvalue())
                response = make_response(encoded)
                response.headers['content-type'] = 'octed-stream'
                response.headers['image-server-format'] = 'bmp'
                return response
            else:
                result = {'result': 'status is not valid.'}
                response = make_response((jsonify(result)))
                response.headers['image-server-status'] = f'{currentStatus}'
                return response

    api.run(host='0.0.0.0', port=restServerPort)

def main(rtspUrl, restServerPort):
    lock = threading.Lock()
    print('starting rstp stream reading...')
    thread = threading.Thread(target=capture, args=[rtspUrl, lock])
    thread.start()
    print('starting http rest server...')
    start_server(restServerPort, lock)

if __name__ == '__main__':
    rtspUrl = os.environ['rtsp_url']
    restServerPortStr = os.environ['rest_server_port']
    restServerPort = 3000
    if restServerPortStr is not None:
        restServerPort = int(restServerPortStr)
    
    print(f'rtsp stream - {rtspUrl}')
    print(f'rest server port - {restServerPort}')

    main(rtspUrl, restServerPort)
