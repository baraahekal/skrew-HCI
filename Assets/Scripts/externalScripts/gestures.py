#!/usr/bin/env python3
import cv2
import mediapipe as mp
import pyautogui
import math
import socket
import time
from mediapipe.tasks.python.vision import GestureRecognizer, GestureRecognizerOptions, RunningMode
from mediapipe.tasks.python import BaseOptions
from mediapipe import Image

# ---------------------------
# Configuration and Globals
# ---------------------------
SEND_DELAY = 0.05  # seconds between sends (~20 FPS)
allowed_gestures = {"Open_Palm", "Victory", "Closed_Fist", "Thumb_Down", "Thumb_Up"}
recognized_label = ""
last_sent_time = 0
CLICK_COOLDOWN = 1.0  # seconds
last_click_time = 0 

# Threshold (normalized) for detecting finger click (tweaking may be required)
click_threshold = 0.04

# ---------------------------
# Socket Setup (as a server)
# ---------------------------
HOST = ''         # Listen on all interfaces.
PORT = 65433      # Use a non-privileged port.
server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server_socket.bind((HOST, PORT))
server_socket.listen(1)
print(f"Gesture server listening on port {PORT}...")
conn, addr = server_socket.accept()
print("Connected by", addr)

# ---------------------------
# Gesture Recognizer Setup
# ---------------------------
model_path = 'gesture_recognizer.task'
gesture_result = None  # to store latest gesture result

def gesture_callback(result, output_image, timestamp_ms):
    global gesture_result
    gesture_result = result

gesture_options = GestureRecognizerOptions(
    base_options=BaseOptions(model_asset_path=model_path),
    running_mode=RunningMode.LIVE_STREAM,
    result_callback=gesture_callback,
)
recognizer = GestureRecognizer.create_from_options(gesture_options)

# ---------------------------
# MediaPipe Hands Setup
# ---------------------------
mp_hands = mp.solutions.hands
hands = mp_hands.Hands(max_num_hands=1, min_detection_confidence=0.7)

# ---------------------------
# Webcam Setup
# ---------------------------
cap = cv2.VideoCapture(0)
cap.set(3, 640)  # Width
cap.set(4, 480)  # Height
if not cap.isOpened():
    print("Error: Cannot access the webcam.")
    exit()

# Get screen dimensions (for optional pyautogui mouse movement)
screen_width, screen_height = pyautogui.size()

# ---------------------------
# Helper: Determine if thumb and index are "touching"
# ---------------------------
def fingers_touching(thumb_tip, index_tip):
    distance = math.sqrt((thumb_tip.x - index_tip.x)**2 + (thumb_tip.y - index_tip.y)**2)
    return distance < click_threshold

# ---------------------------
# Main Loop
# ---------------------------
print("Starting integrated tracking. Press 'q' to quit.")
frame_count = 0

while True:
    ret, frame = cap.read()
    if not ret:
        break

    # Flip frame horizontally for natural interaction.
    frame = cv2.flip(frame, 1)
    rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)

    # Process hand landmarks.
    hand_results = hands.process(rgb_frame)
    index_x, index_y = 0.0, 0.0
    click_triggered = False  # Will be True if thumb and index finger are close enough.

    if hand_results.multi_hand_landmarks:
        hand_landmarks = hand_results.multi_hand_landmarks[0]
        # Get the index fingertip.
        index_tip = hand_landmarks.landmark[mp_hands.HandLandmark.INDEX_FINGER_TIP]
        index_x, index_y = index_tip.x, index_tip.y

        # Also get the thumb fingertip to check for click.
        thumb_tip = hand_landmarks.landmark[mp_hands.HandLandmark.THUMB_TIP]
        if fingers_touching(thumb_tip, index_tip):
             current_time = time.time()
             if current_time - last_click_time >= CLICK_COOLDOWN:
                click_triggered = True
                pyautogui.click()
                last_click_time = current_time

        # Optional: Move the system mouse cursor (if desired).
        x_screen = int(index_x * screen_width)
        y_screen = int(index_y * screen_height)
        pyautogui.moveTo(x_screen, y_screen)

    # Process gesture recognition asynchronously.
    mp_image = Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)
    timestamp = int(cv2.getTickCount() / cv2.getTickFrequency() * 1000)
    recognizer.recognize_async(mp_image, timestamp)

    # Every SEND_DELAY seconds, send the data.
    if time.time() - last_sent_time >= SEND_DELAY:
        # Determine the current gesture label.
        gesture_label = ""
        if gesture_result and gesture_result.gestures:
            top_gesture = gesture_result.gestures[0][0]
            if top_gesture.category_name in allowed_gestures:
                gesture_label = top_gesture.category_name

        # Create the message with gesture, index finger (normalized) coordinates, and click flag.
        msg = f"Gesture:{gesture_label}|X:{index_x:.4f}|Y:{index_y:.4f}|Click:{click_triggered}"
        try:
            conn.sendall(msg.encode('utf-8'))
            print("Sent:", msg)
            last_sent_time = time.time()
        except Exception as send_error:
            print("Error sending data:", send_error)
            break

    # Optionally, show the frame (disable if running headless)
    # cv2.imshow("Integrated Tracking", frame)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

# Cleanup
cap.release()
cv2.destroyAllWindows()
hands.close()
recognizer.close()
conn.close()
server_socket.close()

