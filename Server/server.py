import socket
import threading
import cv2
import numpy as np
import signal
from ultralytics import YOLO

#Variables for holding information about connections
connections = []
total_connections = 0

#Client class, new instance created for each connected client
#Each instance has the socket and address that is associated with items
#Along with an assigned ID and a name chosen by the client
class Client(threading.Thread):
    def __init__(self, socket, address, id, name, signal):
        threading.Thread.__init__(self)
        self.socket = socket
        self.address = address
        self.id = id
        self.name = name
        self.signal = signal
        self.model = YOLO("yolov8m.pt")
    
    def __str__(self):
        return str(self.id) + " " + str(self.address)
    
    # Ensures that we pull the entire expected length of data
    # Normally, for small chunks of data, it should be fine, but larger chunks may need to be pulled in several batches
    def getdataofsize(self,size):
        total_data =[]
        receivedlength=0
        while receivedlength<size:
            data = self.socket.recv(min(size,32000))
            if len(data)==0:
                raise Exception("Socket Closed")
            receivedlength+=len(data)
            # Debug message, uncomment if something goes wrong and the data is not properly received
            #print("Received "+str(len(data))+", Remaining "+str(length-receivedlength))
            total_data.append(data)
        message = b"".join(total_data)
        return message


    def detect_mouse(self, img, scale):
        # Perform object detection
        results = self.model(img)

        # Get the bounding boxes and class labels
        boxes = results[0].boxes.cpu().numpy()
        class_labels = self.model.names

        pois = []

        # Loop through the bounding boxes
        for i, box in enumerate(boxes):
            # Get the coordinates, confidence, and class label
            x1, y1, x2, y2 = box.xyxy[0]
            conf = box.conf[0]
            cls = box.cls[0]
            label = class_labels[int(cls)]

            # Draw the bounding box on the image
            if label == "mouse":
                print("mouse!")
                cv2.rectangle(img, (int(x1), int(y1)), (int(x2), int(y2)), (0, 255, 0), 2)
                poi = (int((x1 + y1)/2 * scale), int((x2 + y2)/2 * scale))
                cv2.circle(img, poi, 3, (255, 255, 255), 2)
                pois.append(poi)
                
        cv2.imwrite("detect.jpg", img)        
        return pois
        

    def processimage(self, message, width, height, canvas_width, canvas_height):
        
        image=np.asarray(bytearray(message), dtype=np.uint8 ).reshape( height,width, 3 )
        cv2.imwrite("uncropped_BGR.jpg", image)
        image_rgb=np.flip(image, axis=-1)
        
        image_rgb=np.flip(image_rgb, axis=0)
        
        image_rgb_rotated = cv2.rotate(image_rgb, cv2.ROTATE_90_COUNTERCLOCKWISE)
        cv2.imwrite("rotated_RGB.jpg", image_rgb_rotated)
        # Get new width and height after processing
        height, width, _ = image_rgb_rotated.shape
        canvas_ratio = canvas_width / canvas_height
        scaled_width = height * canvas_ratio
        scaling_ratio = canvas_height / height
        pix_diff = int(round(abs(scaled_width - width) / 2)) # Div by 2 to pad in from both sides
        image_scaled = image_rgb_rotated[:, pix_diff:-pix_diff, :] # h, w, c

        cv2.imwrite("cropped_RGB.jpg", image_scaled)

        return self.detect_mouse(image_scaled, scaling_ratio)

        # boxes = []
        # drawing = False
        # start_point = (0, 0)
        # def draw_box(event, x, y, flags, param):
        #     nonlocal boxes, drawing, start_point
        #     if event == cv2.EVENT_LBUTTONDOWN:
        #         drawing = True
        #         start_point = (max(0, x), max(0, y))
        #         if len(boxes) == 0 or len(boxes[-1]) == 4:
        #             boxes.append([max(0, x), max(0, y)])
        #         else:
        #             boxes[-1].extend([max(0, x), max(0, y)])
        #     elif event == cv2.EVENT_MOUSEMOVE and flags == cv2.EVENT_FLAG_LBUTTON and drawing:
        #         img = image_scaled.copy()
        #         for box in boxes:
        #             if len(box) == 4:
        #                 cv2.rectangle(img, (box[0], box[1]), (box[2], box[3]), (0, 255, 0), 2)
        #         cv2.rectangle(img, start_point, (x, y), (0, 255, 0), 2)
        #         cv2.imshow("image", img)
        #     elif event == cv2.EVENT_LBUTTONUP:
        #         drawing = False
        #         if len(boxes) > 0 and len(boxes[-1]) == 4:
        #             boxes[-1][2] = max(boxes[-1][0], boxes[-1][2])
        #             boxes[-1][3] = max(boxes[-1][1], boxes[-1][3])
        #             boxes[-1][0] = min(boxes[-1][0], boxes[-1][2])
        #             boxes[-1][1] = min(boxes[-1][1], boxes[-1][3])
        #     elif event == cv2.EVENT_RBUTTONDOWN:
        #         for i, box in enumerate(boxes):
        #             if (box[0] < x < box[2] and box[1] < y < box[3]) or (box[0] < x < box[2] and box[1] < y < box[3]):
        #                 del boxes[i]
        #                 break
        # cv2.namedWindow("image")
        # cv2.setMouseCallback("image", draw_box)
        # while True:
        #     img = image_scaled.copy()
        #     for box in boxes:
        #         if len(box) == 4:
        #             cv2.rectangle(img, (box[0], box[1]), (box[2], box[3]), (0, 255, 0), 2)
        #     cv2.imshow("image", img)
        #     key = cv2.waitKey(1)
        #     if key == 13:  # Enter key
        #         break
        # cv2.destroyWindow("image")
        # centroids = [(int(((x1 + x2) / 2) * scaling_ratio), canvas_height - int(((y1 + y2) / 2) * scaling_ratio)) for x1, y1, x2, y2 in [coord for coord in boxes]]
        # return centroids

    #Attempt to get data from client
    #If unable to, assume client has disconnected and remove him from server data
    #If able to and we get data back, print it in the server and send it back to every
    #client aside from the client that has sent it
    #.decode is used to convert the byte data into a printable string
    def run(self):
        while self.signal:
            try:
                # print("listening")
                # Get all the data from the simple protocol we defined
                datamessagetype = self.getdataofsize(4)
                messagetype = int.from_bytes(datamessagetype, "little")

                if messagetype==0:
                    datalength = self.getdataofsize(4)
                    length = int.from_bytes(datalength, "little")
                    # Debug message, uncomment if something goes wrong and the data is not properly received
                    print("Got "+str(length)+"B from "  + str(self.id))

                    data_img_width = self.getdataofsize(4)
                    img_width = int.from_bytes(data_img_width, "little")
                    data_img_height = self.getdataofsize(4)
                    img_height = int.from_bytes(data_img_height, "little")
                    # Debug message, uncomment if something goes wrong and the data is not properly received
                    print("Received a "+str(img_width)+"x"+str(img_height)+" image")

                    data_canvas_width = self.getdataofsize(4)
                    canvas_width = int.from_bytes(data_canvas_width, "little")
                    data_canvas_height = self.getdataofsize(4)
                    canvas_height = int.from_bytes(data_canvas_height, "little")
                    # Debug message, uncomment if something goes wrong and the data is not properly received
                    print("Canvas size of original image is of dimensions "+str(canvas_width)+"x"+str(canvas_height))

                    message = self.getdataofsize(length)
                    # Debug message, uncomment if something goes wrong and the data is not properly received
                    print("Data received: "+str(len(message))+"B")

                    # Image processing
                    centroids = self.processimage(message, img_width, img_height, canvas_width, canvas_height)
                    
                    replytype = 1
                    datareplytype = replytype.to_bytes(4,'little')
                    print("replytype")

                    replymessage = str(centroids[0]) # Return only the first one for now 
                    replylength = len(replymessage.encode())
                    
                    datareplylength = replylength.to_bytes(4, 'little')

                    print(f"datareplytype: {datareplytype}, replylength: {replylength}, datareplylength: {datareplylength}, datareplymessage: {replymessage}")

                    self.socket.send(datareplytype)
                    self.socket.sendall(datareplylength)
                    self.socket.sendall(replymessage.encode())

            except Exception as e:
                print(e)
                print("Client " + str(self.address) + " has disconnected")
                self.signal = False
                connections.remove(self)
                self.socket.close()  # Add this line to close the socket
                break

#Wait for new connections
def newConnections(socket):
    while True:
        sock, address = socket.accept()
        global total_connections
        connections.append(Client(sock, address, total_connections, "Name", True))
        connections[len(connections) - 1].start()
        print("New connection at ID " + str(connections[len(connections) - 1]))
        total_connections += 1


def main():
    #Get host and port
    host = "0.0.0.0" 
    port = 12345

    #Create new server socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.bind((host, port))
    sock.listen(5)

    print("Server Started")

    #Create new thread to wait for connectionse
    newConnectionsThread = threading.Thread(target = newConnections, args = (sock,))
    newConnectionsThread.start()

if __name__ == "__main__":
    main()