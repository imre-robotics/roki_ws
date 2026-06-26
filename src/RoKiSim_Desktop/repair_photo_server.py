#!/usr/bin/env python3
"""
Tiny HTTP server for phone-to-PC photo transfer in Repair Mode.
Phone connects to same WiFi, opens the URL, takes a photo, and it
instantly appears in the Repair Mode chat.
"""
import http.server
import os
import json
import time
import socket
import sys

UPLOAD_DIR = "/tmp/repair_photos"
PORT = 9090

os.makedirs(UPLOAD_DIR, exist_ok=True)

MOBILE_HTML = """<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
<title>Jarvis Tamir Modu</title>
<style>
* { margin:0; padding:0; box-sizing:border-box; }
body {
    font-family: -apple-system, 'Segoe UI', system-ui, sans-serif;
    background: #0d1117;
    color: #e6edf3; min-height: 100vh;
    display: flex; flex-direction: column; align-items: center;
    padding: 16px; padding-top: 40px;
}
.header {
    text-align: center; margin-bottom: 24px;
}
.header .icon { font-size: 44px; margin-bottom: 8px; }
.header h1 { font-size: 20px; font-weight: 700; color: #fff; }
.header .sub { color: #8b949e; font-size: 13px; margin-top: 4px; }
.status {
    background: #161b22; border-radius: 10px;
    padding: 10px 16px; margin-bottom: 16px; font-size: 13px;
    text-align: center; width: 100%; max-width: 420px;
    border: 1px solid #21262d;
}
.status.ok { border-color: #238636; color: #3fb950; }
.status.err { border-color: #da3633; color: #f85149; }
.card {
    background: #161b22; border: 1px solid #21262d;
    border-radius: 12px; padding: 16px; width: 100%; max-width: 420px;
    margin-bottom: 12px;
}
.btn {
    border: none; color: #fff; font-size: 16px; font-weight: 600;
    padding: 14px 20px; border-radius: 10px; cursor: pointer;
    width: 100%; margin-bottom: 10px;
    transition: transform 0.12s, opacity 0.12s;
    display: flex; align-items: center; justify-content: center; gap: 8px;
}
.btn:active { transform: scale(0.97); opacity: 0.9; }
.btn-primary { background: #7c3aed; }
.btn-secondary { background: #21262d; border: 1px solid #30363d; }
.btn-success { background: #238636; }
.btn-danger { background: #da3633; }
.preview-area {
    width: 100%; border-radius: 8px; overflow: hidden;
    margin-bottom: 12px; display: none;
    border: 1px solid #30363d;
}
.preview-area img { width: 100%; display: block; }
textarea {
    width: 100%; background: #0d1117; color: #e6edf3;
    border: 1px solid #30363d; border-radius: 8px;
    padding: 10px; font-size: 14px; resize: none;
    font-family: inherit; margin-bottom: 12px;
    min-height: 60px;
}
textarea::placeholder { color: #484f58; }
textarea:focus { outline: none; border-color: #7c3aed; }
.spinner { display: none; margin: 10px auto; }
.spinner::after {
    content: ''; display: block; width: 28px; height: 28px;
    border: 3px solid #21262d;
    border-top-color: #7c3aed; border-radius: 50%;
    animation: spin 0.7s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }
input[type=file] { display: none; }
.count {
    text-align: center; color: #484f58; font-size: 12px;
    margin-top: 8px;
}
</style>
</head>
<body>

<div class="header">
    <div class="icon">🔧</div>
    <h1>Jarvis Tamir Modu</h1>
    <p class="sub">Fotoğraf çek, açıklama yaz, Jarvis'e gönder</p>
</div>

<div id="status" class="status">📡 Bağlantı hazır</div>

<div class="card">
    <button class="btn btn-primary" onclick="capturePhoto()">📷 Kamera ile Çek</button>
    <button class="btn btn-secondary" onclick="pickGallery()">🖼️ Galeriden Seç</button>
</div>

<input type="file" id="cameraInput" accept="image/*" capture="environment" onchange="onFileSelected(this)">
<input type="file" id="galleryInput" accept="image/*" onchange="onFileSelected(this)">

<div id="selectedCard" class="card" style="display:none;">
    <div id="preview" class="preview-area"><img id="previewImg"></div>
    <textarea id="description" placeholder="Açıklama ekle (isteğe bağlı)... Örn: Sol taraftaki kablo kopmuş"></textarea>
    <button class="btn btn-success" onclick="sendPhoto()">🚀 Jarvis'e Gönder</button>
    <button class="btn btn-danger" onclick="clearSelection()" style="font-size:14px; padding:10px;">✕ İptal</button>
</div>

<div id="spinner" class="spinner"></div>
<div id="counter" class="count"></div>

<script>
let selectedFile = null;
let sentCount = 0;

function capturePhoto() { document.getElementById('cameraInput').click(); }
function pickGallery() { document.getElementById('galleryInput').click(); }

function clearSelection() {
    selectedFile = null;
    document.getElementById('selectedCard').style.display = 'none';
    document.getElementById('description').value = '';
}

function onFileSelected(input) {
    if (!input.files || !input.files[0]) return;
    selectedFile = input.files[0];
    const reader = new FileReader();
    reader.onload = (e) => {
        document.getElementById('previewImg').src = e.target.result;
        document.getElementById('preview').style.display = 'block';
        document.getElementById('selectedCard').style.display = 'block';
    };
    reader.readAsDataURL(selectedFile);
    // Reset input so same file can be re-selected
    input.value = '';
}

async function sendPhoto() {
    if (!selectedFile) return;
    const status = document.getElementById('status');
    const spinner = document.getElementById('spinner');
    const card = document.getElementById('selectedCard');
    const desc = document.getElementById('description').value.trim();
    
    card.style.display = 'none';
    spinner.style.display = 'block';
    status.textContent = '⏳ Gönderiliyor...';
    status.className = 'status';
    
    try {
        const formData = new FormData();
        formData.append('photo', selectedFile);
        if (desc) formData.append('description', desc);
        
        const resp = await fetch('/upload', { method: 'POST', body: formData });
        const data = await resp.json();
        
        if (data.ok) {
            sentCount++;
            status.textContent = '✅ Gönderildi! Jarvis inceliyor...';
            status.className = 'status ok';
            selectedFile = null;
            document.getElementById('description').value = '';
            document.getElementById('preview').style.display = 'none';
            document.getElementById('counter').textContent = sentCount + ' fotoğraf gönderildi';
            
            // Auto-reset status after 3s
            setTimeout(() => {
                if (status.className === 'status ok') {
                    status.textContent = '📡 Hazır - yeni fotoğraf çekebilirsiniz';
                    status.className = 'status';
                }
            }, 3000);
        } else {
            throw new Error(data.error || 'Bilinmeyen hata');
        }
    } catch(e) {
        status.textContent = '❌ Hata: ' + e.message;
        status.className = 'status err';
        card.style.display = 'block';
    }
    spinner.style.display = 'none';
}
</script>
</body>
</html>"""


class RepairPhotoHandler(http.server.BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass

    def do_GET(self):
        if self.path == "/" or self.path == "/index.html":
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Cache-Control", "no-cache")
            self.end_headers()
            self.wfile.write(MOBILE_HTML.encode("utf-8"))
        elif self.path == "/poll":
            photos = []
            for f in sorted(os.listdir(UPLOAD_DIR)):
                fpath = os.path.join(UPLOAD_DIR, f)
                if os.path.isfile(fpath):
                    photos.append(fpath)
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(json.dumps({"photos": photos}).encode("utf-8"))
        else:
            self.send_error(404)

    def do_POST(self):
        if self.path == "/upload":
            content_length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(content_length)

            content_type = self.headers.get("Content-Type", "")
            if "multipart/form-data" in content_type:
                boundary = content_type.split("boundary=")[1].strip()
                parts = body.split(("--" + boundary).encode())
                
                file_data = None
                description = ""
                
                for part in parts:
                    if b"filename=" in part:
                        header_end = part.find(b"\r\n\r\n")
                        if header_end == -1:
                            continue
                        file_data = part[header_end + 4:]
                        if file_data.endswith(b"\r\n"):
                            file_data = file_data[:-2]
                    elif b'name="description"' in part:
                        header_end = part.find(b"\r\n\r\n")
                        if header_end != -1:
                            desc_data = part[header_end + 4:]
                            if desc_data.endswith(b"\r\n"):
                                desc_data = desc_data[:-2]
                            description = desc_data.decode("utf-8", errors="ignore").strip()
                
                if file_data:
                    timestamp = int(time.time() * 1000)
                    fname = f"phone_{timestamp}.jpg"
                    fpath = os.path.join(UPLOAD_DIR, fname)
                    
                    with open(fpath, "wb") as f:
                        f.write(file_data)
                    
                    # Save description alongside the image
                    if description:
                        desc_path = fpath + ".txt"
                        with open(desc_path, "w") as f:
                            f.write(description)
                    
                    print(f"[REPAIR] Photo: {fpath} ({len(file_data)} bytes) desc: '{description}'")
                    sys.stdout.flush()
                    
                    self.send_response(200)
                    self.send_header("Content-Type", "application/json")
                    self.send_header("Access-Control-Allow-Origin", "*")
                    self.end_headers()
                    self.wfile.write(json.dumps({"ok": True, "path": fpath}).encode())
                    return

            self.send_response(400)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(json.dumps({"ok": False, "error": "Geçersiz dosya"}).encode())
        else:
            self.send_error(404)

    def do_OPTIONS(self):
        """Handle CORS preflight."""
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "POST, GET, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()


def get_local_ip():
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except:
        return "127.0.0.1"


if __name__ == "__main__":
    ip = get_local_ip()
    print(f"REPAIR_PHOTO_SERVER_URL=http://{ip}:{PORT}")
    sys.stdout.flush()
    
    server = http.server.HTTPServer(("0.0.0.0", PORT), RepairPhotoHandler)
    print(f"[REPAIR] Photo server running on http://{ip}:{PORT}")
    sys.stdout.flush()
    
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    server.server_close()
