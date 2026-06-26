#!/usr/bin/env python3
import rclpy
from rclpy.node import Node
from sensor_msgs.msg import Image
from cv_bridge import CvBridge
import cv2
import socket
import threading
import struct

class CameraServer(Node):
    def __init__(self):
        super().__init__('camera_server')
        self.subscription = self.create_subscription(
            Image,
            '/gripper_camera/image_raw',
            self.listener_callback,
            10)
        self.bridge = CvBridge()
        self.current_frame = None
        self.lock = threading.Lock()
        
        # Start TCP Server
        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server_socket.bind(('0.0.0.0', 8003))
        self.server_socket.listen(5)
        self.get_logger().info("Camera TCP Server listening on port 8003...")
        
        self.server_thread = threading.Thread(target=self.accept_clients, daemon=True)
        self.server_thread.start()

    def listener_callback(self, msg):
        try:
            cv_image = self.bridge.imgmsg_to_cv2(msg, "bgr8")
            # Encode to JPEG
            encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 50]
            result, encimg = cv2.imencode('.jpg', cv_image, encode_param)
            if result:
                with self.lock:
                    self.current_frame = encimg.tobytes()
        except Exception as e:
            self.get_logger().error(f"CV Bridge Error: {e}")

    def accept_clients(self):
        while True:
            try:
                client, addr = self.server_socket.accept()
                self.get_logger().info(f"Client connected: {addr}")
                client_thread = threading.Thread(target=self.handle_client, args=(client,), daemon=True)
                client_thread.start()
            except Exception as e:
                self.get_logger().error(f"Accept Error: {e}")

    def handle_client(self, client):
        try:
            while True:
                with self.lock:
                    frame = self.current_frame
                if frame:
                    # Send size first (4 bytes), then frame
                    size = len(frame)
                    client.sendall(struct.pack("<L", size))
                    client.sendall(frame)
                else:
                    # Send 0 size if no frame
                    client.sendall(struct.pack("<L", 0))
                
                # Sleep a bit to limit frame rate (~30fps)
                import time
                time.sleep(0.033)
        except Exception as e:
            self.get_logger().info(f"Client disconnected: {e}")
        finally:
            client.close()

def main(args=None):
    rclpy.init(args=args)
    node = CameraServer()
    rclpy.spin(node)
    node.destroy_node()
    rclpy.shutdown()

if __name__ == '__main__':
    main()
