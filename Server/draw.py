import cv2
import numpy as np

# Load the image
img = cv2.imread('image.jpg')

# Create a copy of the image
img_copy = img.copy()

# Define the callback function for mouse events
def draw_bounding_box(event, x, y, flags, param):
    global img_copy, x1, y1, points
    if event == cv2.EVENT_LBUTTONDOWN:
        cv2.circle(img_copy, (x, y), 5, (0, 0, 255), -1)
        cv2.imshow('Image', img_copy)
        x1, y1 = x, y
    elif event == cv2.EVENT_LBUTTONUP:
        cv2.circle(img_copy, (x, y), 5, (0, 0, 255), -1)
        cv2.imshow('Image', img_copy)
        points.append(((x1, y1), (x, y)))
    elif event == cv2.EVENT_MOUSEMOVE and flags == cv2.EVENT_FLAG_LBUTTON:
        img_copy = img.copy()
        for point in points:
            cv2.rectangle(img_copy, point[0], point[1], (0, 255, 0), 2)
        cv2.rectangle(img_copy, (x1, y1), (x, y), (0, 255, 0), 2)
        cv2.imshow('Image', img_copy)
    elif event == cv2.EVENT_RBUTTONDOWN:
        if points:
            min_distance = float('inf')
            min_index = -1
            for i, point in enumerate(points):
                x2, y2 = (point[0][0] + point[1][0]) // 2, (point[0][1] + point[1][1]) // 2
                distance = (x2 - x) ** 2 + (y2 - y) ** 2
                if distance < min_distance:
                    min_distance = distance
                    min_index = i
            if min_index != -1:
                points.pop(min_index)
                img_copy = img.copy()
                for point in points:
                    cv2.rectangle(img_copy, point[0], point[1], (0, 255, 0), 2)
                cv2.imshow('Image', img_copy)

# Set up the window and callback
cv2.namedWindow('Image')
cv2.setMouseCallback('Image', draw_bounding_box)

# Initialize the points list
points = []

# Start the main loop
while True:
    cv2.imshow('Image', img_copy)
    key = cv2.waitKey(1) & 0xFF
    if key == ord("c"):
        break
    elif key == ord("r"):
        img_copy = img.copy()
        points = []
    elif key == 13:  # Enter key
        print("Bounding box coordinates:", points)
        points = []

cv2.destroyAllWindows()