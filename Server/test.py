import cv2
from ultralytics import YOLO

# Load the YOLOv8 model
model = YOLO("yolov8m.pt")

# Load the image
img = cv2.imread("image.jpg")

# Perform object detection
results = model(img)

# Get the bounding boxes and class labels
boxes = results[0].boxes.cpu().numpy()
class_labels = model.names

# Loop through the bounding boxes
for i, box in enumerate(boxes):
    # Get the coordinates, confidence, and class label
    x1, y1, x2, y2 = box.xyxy[0]
    conf = box.conf[0]
    cls = box.cls[0]
    label = class_labels[int(cls)]

    # Draw the bounding box on the image
    if label == "mouse":
        cv2.rectangle(img, (int(x1), int(y1)), (int(x2), int(y2)), (0, 255, 0), 2)
        cv2.putText(img, f"{label} {conf:.2f}", (int(x1), int(y1) - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 255, 0), 2)

# Display the output
cv2.imshow("Output", img)
cv2.waitKey(0)
cv2.destroyAllWindows()