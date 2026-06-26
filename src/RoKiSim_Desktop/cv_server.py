import cv2
import mediapipe as mp
import socket
import json
import struct
import math
import sys
import time

def main():
    port = 5050
    print(f"Starting CV Server on port {port}...")
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_socket.bind(('127.0.0.1', port))
    server_socket.listen(1)

    mp_hands = mp.solutions.hands
    hands = mp_hands.Hands(min_detection_confidence=0.7, min_tracking_confidence=0.7, max_num_hands=2)
    mp_draw = mp.solutions.drawing_utils

    while True:
        print("Waiting for connection...")
        conn, addr = server_socket.accept()
        print(f"Connected by {addr}")
        
        cap = cv2.VideoCapture(0)
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        cap.set(cv2.CAP_PROP_BUFFERSIZE, 1) # Reduce lag by keeping only the latest frame

        try:
            while cap.isOpened():
                success, frame = cap.read()
                if not success:
                    break

                # Flip frame horizontally for a selfie-view display
                frame = cv2.flip(frame, 1)
                rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                results = hands.process(rgb_frame)

                data = {"rx": -1, "ry": -1, "r_size": -1, "r_angle": -1, "lx": -1, "ly": -1, "pinch": -1}

                if results.multi_hand_landmarks and results.multi_handedness:
                    for hand_landmarks, handedness in zip(results.multi_hand_landmarks, results.multi_handedness):
                        label = handedness.classification[0].label # 'Left' or 'Right'
                        # Flipped image means MediaPipe's prediction is reversed
                        actual_hand = 'Right' if label == 'Left' else 'Left'
                        
                        mp_draw.draw_landmarks(frame, hand_landmarks, mp_hands.HAND_CONNECTIONS)
                        
                        if actual_hand == 'Right':
                            # Use wrist (0) for position
                            wrist = hand_landmarks.landmark[mp_hands.HandLandmark.WRIST]
                            data["rx"] = wrist.x
                            data["ry"] = wrist.y
                            
                            # Size: distance from wrist to middle finger tip (depth proxy)
                            middle = hand_landmarks.landmark[mp_hands.HandLandmark.MIDDLE_FINGER_TIP]
                            data["r_size"] = math.sqrt((wrist.x - middle.x)**2 + (wrist.y - middle.y)**2 + (wrist.z - middle.z)**2)
                            
                            # Angle: 2D angle of wrist to middle finger (roll proxy)
                            data["r_angle"] = math.atan2(middle.y - wrist.y, middle.x - wrist.x)
                        
                        if actual_hand == 'Left':
                            # Get wrist for X/Y position
                            wrist = hand_landmarks.landmark[mp_hands.HandLandmark.WRIST]
                            data["lx"] = wrist.x
                            data["ly"] = wrist.y

                            # Use thumb (4) and index (8) for pinch
                            thumb = hand_landmarks.landmark[mp_hands.HandLandmark.THUMB_TIP]
                            index = hand_landmarks.landmark[mp_hands.HandLandmark.INDEX_FINGER_TIP]
                            dist = math.sqrt((thumb.x - index.x)**2 + (thumb.y - index.y)**2 + (thumb.z - index.z)**2)
                            data["pinch"] = dist
                            
                            # Draw line between them
                            h, w, c = frame.shape
                            cx1, cy1 = int(thumb.x * w), int(thumb.y * h)
                            cx2, cy2 = int(index.x * w), int(index.y * h)
                            cv2.line(frame, (cx1, cy1), (cx2, cy2), (255, 0, 255), 3)

                # Encode image
                ret, buffer = cv2.imencode('.jpg', frame, [int(cv2.IMWRITE_JPEG_QUALITY), 60])
                if not ret:
                    continue
                jpg_bytes = buffer.tobytes()
                
                # Encode json
                json_str = json.dumps(data)
                json_bytes = json_str.encode('utf-8')

                # Pack: JSON Length (4), JSON Bytes, JPEG Length (4), JPEG Bytes
                payload = struct.pack('<I', len(json_bytes)) + json_bytes + struct.pack('<I', len(jpg_bytes)) + jpg_bytes
                
                conn.sendall(payload)

        except Exception as e:
            print(f"Connection lost or error: {e}")
        finally:
            cap.release()
            conn.close()

if __name__ == "__main__":
    main()
