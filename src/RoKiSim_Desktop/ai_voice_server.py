import socket
import json
import threading
import time
import urllib.request
import urllib.parse
import re
import os
import sys

try:
    import google.generativeai as genai
    HAS_GENAI = True
except ImportError:
    HAS_GENAI = False

import subprocess
import speech_recognition as sr

def speak_text(text, conn=None):
    def run():
        if conn:
            try: conn.sendall((json.dumps({"status": "speaking"}) + "\n").encode('utf-8'))
            except: pass
        try:
            safe_text = text.replace('"', '').replace("'", "")
            cmd = f'python3 -m edge_tts --voice en-GB-RyanNeural --text "{safe_text}" --write-media /tmp/tts.mp3 && /usr/bin/ffplay -nodisp -autoexit -loglevel quiet /tmp/tts.mp3'
            subprocess.run(cmd, shell=True)
        except Exception as e:
            print("TTS error:", e)
        finally:
            if conn:
                try: conn.sendall((json.dumps({"status": "idle"}) + "\n").encode('utf-8'))
                except: pass
    threading.Thread(target=run, daemon=True).start()

PORT = 8002

def translate_text(text, target_lang="tr"):
    try:
        url = f"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={target_lang}&dt=t&q={urllib.parse.quote(text)}"
        req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
        with urllib.request.urlopen(req) as response:
            result = json.loads(response.read().decode())
            return result[0][0][0]
    except Exception as e:
        return f"Translation failed: {e}"

def mock_process_message(msg, level):
    msg_lower = msg.lower()
    
    if msg_lower.startswith("cevir:") or msg_lower.startswith("çevir:") or msg_lower.startswith("translate:"):
        text_to_translate = msg[msg.find(":") + 1:].strip()
        translated = translate_text(text_to_translate, target_lang="tr")
        return f"[TÜRKÇE ÇEVİRİ]: {translated}", None
        
    elif msg_lower.startswith("en:"):
        text_to_translate = msg[msg.find(":") + 1:].strip()
        translated = translate_text(text_to_translate, target_lang="en")
        return f"[ENGLISH]: {translated}", None

    m = re.search(r'(?:joint|eksen|j)\s*([1-7])\s*(?:to|yap|olsun|:)?\s*(-?\d+)', msg_lower)
    if not m:
        m = re.search(r'([1-7])\.?\s*(?:eksen|joint|j)\s*(?:to|yap|olsun|:)?\s*(-?\d+)', msg_lower)
    if m:
        joint_num = int(m.group(1))
        val = float(m.group(2))
        return f"Okay, moving joint {joint_num} to {val}.", {"type": "JOG_JOINT", "joint": joint_num, "value": val}

    m = re.search(r'(?:move|jog|git|sür|hareket)\s*([xyz])\s*(?:by|ekseninde)?\s*(-?\d+)', msg_lower)
    if not m:
        m = re.search(r'([xyz])\s*(?:ekseninde|axis)?\s*(?:move|jog|git|sür|hareket|by)?\s*(-?\d+)', msg_lower)
    if m:
        axis = m.group(1).upper()
        val = float(m.group(2))
        return f"Okay, jogging cartesian {axis} by {val} mm.", {"type": "JOG_CART", "axis": axis, "value": val}

    if "hello" in msg_lower or "hi " in msg_lower:
        return "Hello! I am ready to assist you.", None
    elif "open" in msg_lower and ("gripper" in msg_lower or "jaw" in msg_lower) or "kıskaç" in msg_lower and "aç" in msg_lower:
        return "Okay, opening the gripper.", {"type": "GRIPPER_OPEN"}
    elif "close" in msg_lower and ("gripper" in msg_lower or "jaw" in msg_lower) or "kıskaç" in msg_lower and "kapat" in msg_lower:
        return "Okay, closing the gripper.", {"type": "GRIPPER_CLOSE"}
    elif "home" in msg_lower or "eve dön" in msg_lower or "sıfırla" in msg_lower:
        return "Moving the robot arm to the home position.", {"type": "MOVE_HOME"}
    elif "record" in msg_lower or "kayıt" in msg_lower or "kaydet" in msg_lower:
        if "start" in msg_lower or "başlat" in msg_lower:
            return "Starting recording...", {"type": "START_RECORDING"}
        elif "stop" in msg_lower or "durdur" in msg_lower or "bitir" in msg_lower:
            return "Stopping recording...", {"type": "STOP_RECORDING"}
        elif "play" in msg_lower or "oynat" in msg_lower:
            return "Playing back recording...", {"type": "PLAY_RECORDING"}
    
    if "kolayı uzat" in msg_lower or "şişeyi uzat" in msg_lower:
        return "Tabii ki efendim, kolayı hemen getiriyorum.", {"type": "VISION_PICK", "object": "bottle"}
    if "çekici uzat" in msg_lower:
        return "Tabii ki efendim, çekici hemen getiriyorum.", {"type": "VISION_PICK", "object": "hammer"}
    if "kaseyi uzat" in msg_lower:
        return "Tabii ki efendim, kaseyi hemen getiriyorum.", {"type": "VISION_PICK", "object": "bowl"}

    if re.search(r'tamir modu', msg_lower) or re.search(r'tamir modunu ba[sş]lat', msg_lower):
        return "Tamir modunu başlatıyorum. Lütfen arızalı parçanın fotoğrafını yükleyin.", {"type": "START_REPAIR_MODE"}

    elif "mcu" in msg_lower:
        if "connect" in msg_lower or "bağlan" in msg_lower:
            return "Connecting to MCU...", {"type": "CONNECT_MCU"}
        elif "disconnect" in msg_lower or "kopar" in msg_lower or "kes" in msg_lower:
            return "Disconnecting MCU...", {"type": "DISCONNECT_MCU"}
    elif "opc" in msg_lower:
        if "connect" in msg_lower or "bağlan" in msg_lower:
            return "Connecting to OPC UA...", {"type": "CONNECT_OPC"}
        elif "disconnect" in msg_lower or "kopar" in msg_lower or "kes" in msg_lower:
            return "Disconnecting OPC UA...", {"type": "DISCONNECT_OPC"}
    elif "gamepad" in msg_lower:
        return "Toggling Gamepad control.", {"type": "TOGGLE_GAMEPAD"}
    elif "vision" in msg_lower or "görüntü" in msg_lower:
        return "Toggling Computer Vision override.", {"type": "TOGGLE_CV"}

    return f"[MOCK AI] I heard: {msg}. Please paste an API Key into api_key.txt to chat with real AI!", None


# Global chat history for Gemini
gemini_chat_sessions = {}

def get_system_prompt(level):
    return f"""You are an advanced AI assistant named Jarvis for a robotic arm simulator (RoKiSim).
The user will chat with you in English (or Turkish if they need help).
Your job is to respond naturally to the user based on their English level: {level}. You are highly intelligent, polite, and concise, much like Jarvis from Iron Man.

If the user's message contains an implicit or explicit command for the robot, you must output a JSON object containing the text response and the command object.
The supported commands are:
- {{"type": "GRIPPER_OPEN"}}
- {{"type": "GRIPPER_CLOSE"}}
- {{"type": "MOVE_HOME"}}
- {{"type": "JOG_JOINT", "joint": <1-7>, "value": <target_angle>}}
- {{"type": "JOG_CART", "axis": "<X/Y/Z>", "value": <distance_mm>}}
- {{"type": "START_RECORDING"}}
- {{"type": "STOP_RECORDING"}}
- {{"type": "PLAY_RECORDING"}}
- {{"type": "CONNECT_MCU"}}
- {{"type": "DISCONNECT_MCU"}}
- {{"type": "CONNECT_OPC"}}
- {{"type": "DISCONNECT_OPC"}}
- {{"type": "TOGGLE_GAMEPAD"}}
- {{"type": "TOGGLE_CV"}}
- {{"type": "TOGGLE_WORKSPACE"}}
- {{"type": "SHOW_AXES"}}
- {{"type": "OPEN_SCRIPTING"}}
- {{"type": "HELP_TOUR"}}
- {{"type": "CONFIG_GAMEPAD"}}
- {{"type": "SHOW_TELEMETRY_POS"}}
- {{"type": "SHOW_TELEMETRY_TORQUE"}}
- {{"type": "SHOW_TELEMETRY_TCP"}}
- {{"type": "EXPORT_CSV"}}
- {{"type": "VISION_PICK", "object": "<object_name>"}} (Use this if the user asks you to give/bring them an object like hammer, bottle, bowl, or cup. Just reply with 'Okay, I will get the <object> for you' and emit this command)
- {{"type": "START_REPAIR_MODE"}} (Use this if the user says 'tamir modunu başlat', 'tamir modu', or asks for help repairing something. Reply in Turkish that you are starting repair mode and ask them to upload a photo.)

You MUST ALWAYS return a JSON dictionary exactly in this format (nothing else):
{{
  "response": "Your natural language response here...",
  "command": {{ "type": "GRIPPER_OPEN" }} 
}}
If there is no command to execute, simply omit the "command" key. Do not wrap the JSON in markdown code blocks.
"""

def process_with_gemini(msg, level, api_key):
    if api_key not in gemini_chat_sessions:
        genai.configure(api_key=api_key)
        model = genai.GenerativeModel(
            model_name="gemini-2.5-flash",
            system_instruction=get_system_prompt(level)
        )
        gemini_chat_sessions[api_key] = model.start_chat(history=[])
    
    chat = gemini_chat_sessions[api_key]
    
    # Check manual translation override first
    msg_lower = msg.lower()
    if msg_lower.startswith("cevir:") or msg_lower.startswith("çevir:") or msg_lower.startswith("translate:"):
        text_to_translate = msg[msg.find(":") + 1:].strip()
        translated = translate_text(text_to_translate, target_lang="tr")
        return f"[TÜRKÇE ÇEVİRİ]: {translated}", None
    elif msg_lower.startswith("en:"):
        text_to_translate = msg[msg.find(":") + 1:].strip()
        translated = translate_text(text_to_translate, target_lang="en")
        return f"[ENGLISH]: {translated}", None

    try:
        response = chat.send_message(msg)
        response_text = response.text.strip()
        
        # Parse JSON from Gemini response
        if response_text.startswith("```json"):
            response_text = response_text[7:-3].strip()
        elif response_text.startswith("```"):
            response_text = response_text[3:-3].strip()
            
        data = json.loads(response_text)
        resp_str = data.get("response", "...")
        cmd_obj = data.get("command", None)
        return resp_str, cmd_obj
    except Exception as e:
        print("Gemini API Error:", e)
        return mock_process_message(msg, level)


def handle_client(conn, addr):
    print(f"Connected by {addr}")
    try:
        while True:
            data = conn.recv(1024)
            if not data:
                break
            
            payload = data.decode('utf-8').strip()
            if not payload: continue
            
            try:
                req = json.loads(payload)
                msg = req.get("msg", "")
                level = req.get("level", "Beginner")
                audio_path = req.get("audio", "")
                
                if audio_path and os.path.exists(audio_path):
                    r = sr.Recognizer()
                    try:
                        with sr.AudioFile(audio_path) as source:
                            audio_data = r.record(source)
                        try:
                            msg = r.recognize_google(audio_data, language="tr-TR")
                        except sr.UnknownValueError:
                            msg = r.recognize_google(audio_data, language="en-US")
                    except Exception as e:
                        print(f"Speech recognition error: {e}")
                        
                if not msg.strip():
                    continue
                script_dir = os.path.dirname(os.path.abspath(__file__))
                api_key_path = os.path.join(script_dir, "api_key.txt")
                api_key = None
                if os.path.exists(api_key_path):
                    with open(api_key_path, "r") as f:
                        api_key = f.read().strip()
                
                if api_key and HAS_GENAI:
                    resp_text, cmd_obj = process_with_gemini(msg, level, api_key)
                else:
                    reason = ""
                    if not api_key:
                        reason += "API Key not found in api_key.txt. (Current dir: " + os.getcwd() + ") "
                    if not HAS_GENAI:
                        reason += "google.generativeai library not installed. "
                    
                    resp_text, cmd_obj = mock_process_message(msg, level)
                    # Append the reason to the mock response so we know why it fell back
                    resp_text = resp_text + f" [DEBUG: {reason}]"
                
                resp_obj = {"response": resp_text}
                if cmd_obj:
                    resp_obj["command"] = json.dumps(cmd_obj)
                    
                conn.sendall((json.dumps(resp_obj) + "\n").encode('utf-8'))
                speak_text(resp_text, conn)
                
            except Exception as e:
                print("Error parsing msg:", e)
                
    except Exception as e:
        print(f"Error handling client: {e}")
    finally:
        conn.close()

def main():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('127.0.0.1', PORT))
    s.listen(1)
    print(f"AI Partner Server listening on port {PORT}")
    
    while True:
        try:
            conn, addr = s.accept()
            t = threading.Thread(target=handle_client, args=(conn, addr))
            t.daemon = True
            t.start()
        except KeyboardInterrupt:
            break
            
    s.close()

if __name__ == '__main__':
    main()
