#!/usr/bin/env python3
"""
MiMo TTS Studio - 语音合成调试工具
浏览器 GUI，暴露所有参数供调节

用法:
  python3 scripts/tts_studio.py
  然后浏览器打开 http://localhost:8765
"""

import http.server
import json
import os
import base64
import subprocess
import sys
import urllib.request
import ssl
import threading
import webbrowser
from pathlib import Path

API_BASE = "https://api.xiaomimimo.com/v1"
API_KEY = os.environ.get("MIMO_API_KEY", "sk-cgllm3las2ivx5ea6qcswqd05mgdagkmy9vt4e3sdmdl0gzc")
PORT = 8765
OUTPUT_DIR = Path(__file__).parent.parent / "tmp" / "tts_studio"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

HTML_PAGE = r"""<!DOCTYPE html>
<html lang="zh">
<head>
<meta charset="UTF-8">
<title>MiMo TTS Studio</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { font-family: 'Segoe UI', 'Microsoft YaHei', sans-serif; background: #1a1a2e; color: #e0e0e0; padding: 20px; }
  h1 { color: #00d4ff; margin-bottom: 20px; font-size: 24px; }
  h1 span { color: #888; font-size: 14px; font-weight: normal; }
  .container { display: flex; gap: 20px; max-width: 1200px; }
  .panel { background: #16213e; border-radius: 12px; padding: 20px; flex: 1; }
  .panel h2 { color: #00d4ff; font-size: 16px; margin-bottom: 15px; border-bottom: 1px solid #333; padding-bottom: 8px; }
  label { display: block; margin-bottom: 4px; color: #aaa; font-size: 13px; }
  select, input[type="text"], textarea {
    width: 100%; padding: 8px 10px; margin-bottom: 12px; border: 1px solid #333;
    border-radius: 6px; background: #0f3460; color: #e0e0e0; font-size: 14px;
  }
  textarea { height: 100px; resize: vertical; font-family: monospace; }
  select:focus, input:focus, textarea:focus { outline: none; border-color: #00d4ff; }
  .row { display: flex; gap: 10px; }
  .row > div { flex: 1; }
  button {
    padding: 10px 24px; border: none; border-radius: 6px; cursor: pointer;
    font-size: 14px; font-weight: bold; transition: all 0.2s;
  }
  .btn-primary { background: #00d4ff; color: #000; }
  .btn-primary:hover { background: #00b8e6; }
  .btn-primary:disabled { background: #444; color: #888; cursor: not-allowed; }
  .btn-secondary { background: #e94560; color: #fff; }
  .btn-secondary:hover { background: #d63851; }
  .btn-save { background: #4ecca3; color: #000; }
  .btn-save:hover { background: #45b892; }
  .actions { display: flex; gap: 10px; margin-top: 10px; }
  .status { margin-top: 10px; padding: 10px; background: #0f3460; border-radius: 6px; font-size: 13px; min-height: 40px; }
  .status.error { border-left: 3px solid #e94560; }
  .status.ok { border-left: 3px solid #4ecca3; }
  .presets { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 12px; }
  .preset {
    padding: 4px 10px; background: #0f3460; border: 1px solid #333; border-radius: 14px;
    cursor: pointer; font-size: 12px; transition: all 0.2s;
  }
  .preset:hover { border-color: #00d4ff; color: #00d4ff; }
  .preset.active { background: #00d4ff; color: #000; border-color: #00d4ff; }
  #history { max-height: 400px; overflow-y: auto; }
  .history-item {
    padding: 8px; margin-bottom: 6px; background: #0f3460; border-radius: 6px;
    cursor: pointer; font-size: 12px; display: flex; justify-content: space-between; align-items: center;
  }
  .history-item:hover { background: #1a4a7a; }
  .history-item .meta { color: #888; }
  .history-item .text { flex: 1; margin: 0 10px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  audio { width: 100%; margin-top: 10px; }
  .tag-input { display: flex; flex-wrap: wrap; gap: 4px; margin-bottom: 12px; padding: 6px; background: #0f3460; border-radius: 6px; border: 1px solid #333; }
  .tag { padding: 2px 8px; background: #00d4ff22; border: 1px solid #00d4ff66; border-radius: 10px; font-size: 12px; display: flex; align-items: center; gap: 4px; }
  .tag .x { cursor: pointer; color: #e94560; font-weight: bold; }
  .tag-input input { border: none; background: transparent; color: #e0e0e0; outline: none; flex: 1; min-width: 80px; font-size: 13px; }
  .hint { font-size: 11px; color: #666; margin-bottom: 12px; }
</style>
</head>
<body>
<h1>MiMo TTS Studio <span>mimo-v2-tts 语音合成调试</span></h1>

<div class="container">
  <div class="panel" style="flex: 1.5;">
    <h2>参数面板</h2>

    <label>音色 (Voice)</label>
    <select id="voice" onchange="onVoiceChange()">
      <option value="default_zh" selected>MiMo-中文女声</option>
      <option value="default_en">MiMo-英文女声</option>
      <option value="mimo_default">MiMo-默认</option>
      <option value="mimo_male_firm">男声-坚定军人</option>
      <option value="mimo_male_wild">男声-狂野粗犷</option>
      <option value="mimo_male_calm">男声-沉稳大叔</option>
    </select>

    <label>风格标签 (Style)</label>
    <div class="hint">输入后按 Enter 添加标签，会自动拼接为 &lt;style&gt;标签1 标签2&lt;/style&gt;</div>
    <div class="tag-input" id="tagContainer" onclick="document.getElementById('tagInput').focus()">
      <span id="tagSlots"></span>
      <input type="text" id="tagInput" placeholder="输入风格词按回车...">
    </div>
    <div class="presets">
      <span class="preset" onclick="addTags('冷静','专业')">冷静专业</span>
      <span class="preset" onclick="addTags('温柔','空灵')">温柔空灵</span>
      <span class="preset" onclick="addTags('狂野','粗犷')">狂野粗犷</span>
      <span class="preset" onclick="addTags('坚定','军人')">坚定军人</span>
      <span class="preset" onclick="addTags('Whisper')">Whisper</span>
      <span class="preset" onclick="addTags('Happy')">Happy</span>
      <span class="preset" onclick="addTags('Sad')">Sad</span>
      <span class="preset" onclick="addTags('Angry')">Angry</span>
      <span class="preset" onclick="addTags('唱歌')">唱歌</span>
      <span class="preset" onclick="addTags('东北方言')">东北方言</span>
      <span class="preset" onclick="addTags('四川方言')">四川方言</span>
      <span class="preset" onclick="addTags('粤语')">粤语</span>
      <span class="preset" onclick="addTags('台湾腔')">台湾腔</span>
      <span class="preset" onclick="addTags('夹子音')">夹子音</span>
    </div>

    <div class="row">
      <div>
        <label>User 消息 (可选，影响语气)</label>
        <input type="text" id="userMsg" value="请用角色配音的语气说下面这句台词">
      </div>
    </div>

    <label>合成文本 (将放入 assistant 角色)</label>
    <textarea id="synthText" placeholder="输入要合成的文本...">入侵完成。防火墙已被绕过，目标系统已暴露。</textarea>

    <div class="row">
      <div>
        <label>音频格式</label>
        <select id="audioFormat">
          <option value="wav">WAV (非流式)</option>
          <option value="pcm16">PCM16 (流式拼接)</option>
        </select>
      </div>
      <div>
        <label>请求模式</label>
        <select id="streamMode">
          <option value="false">非流式 (推荐)</option>
          <option value="true">流式</option>
        </select>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" id="btnGenerate" onclick="generate()">生成语音</button>
      <button class="btn-secondary" id="btnPlay" onclick="playAudio()" disabled>播放</button>
      <button class="btn-save" id="btnSave" onclick="saveAudio()" disabled>保存 WAV</button>
    </div>

    <div class="status" id="status">就绪。调节参数后点击"生成语音"。</div>
  </div>

  <div class="panel">
    <h2>生成历史</h2>
    <div id="history"></div>
  </div>
</div>

<audio id="player" controls style="display:none; margin-top:20px; max-width:1200px; width:100%;"></audio>

<script>
let tags = [];
let lastAudioUrl = null;
let history = [];

// 男声映射: 虚拟音色名 -> { apiVoice, styleTags }
const MALE_VOICE_MAP = {
  'mimo_male_firm': { voice: 'mimo_default', tags: ['坚定', '浑厚', '军人', '男性'] },
  'mimo_male_wild': { voice: 'mimo_default', tags: ['狂野', '粗犷', '狂笑', '男性'] },
  'mimo_male_calm': { voice: 'mimo_default', tags: ['沉稳', '低沉', '中年', '男性'] },
};

function onVoiceChange() {
  const v = document.getElementById('voice').value;
  if (MALE_VOICE_MAP[v]) {
    tags = [...MALE_VOICE_MAP[v].tags];
    renderTags();
  }
}

function getRealVoice() {
  const v = document.getElementById('voice').value;
  if (MALE_VOICE_MAP[v]) return MALE_VOICE_MAP[v].voice;
  return v;
}

function renderTags() {
  document.getElementById('tagSlots').innerHTML = tags.map((t, i) =>
    `<span class="tag">${t}<span class="x" onclick="removeTag(${i})">&times;</span></span>`
  ).join('');
}

function addTags(...newTags) {
  newTags.forEach(t => { if (!tags.includes(t)) tags.push(t); });
  renderTags();
}

function removeTag(i) {
  tags.splice(i, 1);
  renderTags();
}

document.getElementById('tagInput').addEventListener('keydown', function(e) {
  if (e.key === 'Enter' && this.value.trim()) {
    e.preventDefault();
    addTags(...this.value.trim().split(/\s+/));
    this.value = '';
  }
});

function getStyleStr() {
  return tags.length ? `<style>${tags.join(' ')}</style>` : '';
}

async function generate() {
  const btn = document.getElementById('btnGenerate');
  const status = document.getElementById('status');
  btn.disabled = true;
  status.className = 'status';
  status.textContent = '生成中...';

  const payload = {
    model: 'mimo-v2-tts',
    messages: [
      { role: 'user', content: document.getElementById('userMsg').value },
      { role: 'assistant', content: getStyleStr() + document.getElementById('synthText').value },
    ],
    audio: {
      format: document.getElementById('audioFormat').value,
      voice: getRealVoice(),
    },
    stream: document.getElementById('streamMode').value === 'true',
  };

  try {
    const resp = await fetch('/api/tts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const data = await resp.json();
    if (data.error) throw new Error(data.error);

    lastAudioUrl = data.url;
    const player = document.getElementById('player');
    player.src = data.url;
    player.style.display = 'block';
    player.play();

    document.getElementById('btnPlay').disabled = false;
    document.getElementById('btnSave').disabled = false;
    status.className = 'status ok';
    status.textContent = `生成成功! ${data.size} bytes | ${data.duration_ms}ms`;

    history.unshift({
      text: document.getElementById('synthText').value.substring(0, 60),
      voice: getRealVoice(),
      style: tags.join(' '),
      url: data.url,
      size: data.size,
    });
    renderHistory();
  } catch (e) {
    status.className = 'status error';
    status.textContent = '错误: ' + e.message;
  }
  btn.disabled = false;
}

function playAudio() {
  const player = document.getElementById('player');
  if (player.src) { player.currentTime = 0; player.play(); }
}

function saveAudio() {
  if (!lastAudioUrl) return;
  const a = document.createElement('a');
  a.href = lastAudioUrl;
  a.download = 'tts_output.wav';
  a.click();
}

function playFromHistory(url) {
  const player = document.getElementById('player');
  player.src = url;
  player.style.display = 'block';
  player.play();
}

function renderHistory() {
  document.getElementById('history').innerHTML = history.map((h, i) =>
    `<div class="history-item" onclick="playFromHistory('${h.url}')">
      <span class="meta">${h.voice}</span>
      <span class="text">${h.text}</span>
      <span class="meta">${(h.size/1024).toFixed(0)}KB</span>
    </div>`
  ).join('');
}
</script>
</body>
</html>
"""

counter = 0


class TTSHandler(http.server.BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        pass  # quiet

    def do_GET(self):
        if self.path == "/" or self.path == "/index.html":
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.end_headers()
            self.wfile.write(HTML_PAGE.encode())
        elif self.path.startswith("/audio/"):
            fname = self.path.split("/audio/")[1]
            fpath = OUTPUT_DIR / fname
            if fpath.exists():
                self.send_response(200)
                self.send_header("Content-Type", "audio/wav")
                self.end_headers()
                self.wfile.write(fpath.read_bytes())
            else:
                self.send_error(404)
        else:
            self.send_error(404)

    def do_POST(self):
        if self.path == "/api/tts":
            length = int(self.headers.get("Content-Length", 0))
            body = json.loads(self.rfile.read(length))
            try:
                import time
                global counter
                counter += 1
                t0 = time.time()

                req = urllib.request.Request(
                    f"{API_BASE}/chat/completions",
                    data=json.dumps(body).encode(),
                    headers={
                        "api-key": API_KEY,
                        "Content-Type": "application/json",
                    },
                )
                ctx = ssl.create_default_context()
                ctx.check_hostname = False
                ctx.verify_mode = ssl.CERT_NONE
                resp = urllib.request.urlopen(req, context=ctx, timeout=60)
                data = json.loads(resp.read().decode())

                audio_b64 = data["choices"][0]["message"]["audio"]["data"]
                audio_bytes = base64.b64decode(audio_b64)

                fname = f"studio_{counter:03d}.wav"
                (OUTPUT_DIR / fname).write_bytes(audio_bytes)

                elapsed = int((time.time() - t0) * 1000)
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(json.dumps({
                    "url": f"/audio/{fname}",
                    "size": len(audio_bytes),
                    "duration_ms": elapsed,
                }).encode())

            except Exception as e:
                self.send_response(500)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(json.dumps({"error": str(e)}).encode())
        else:
            self.send_error(404)


def main():
    server = http.server.HTTPServer(("0.0.0.0", PORT), TTSHandler)
    url = f"http://localhost:{PORT}"
    print(f"MiMo TTS Studio 启动: {url}")
    print(f"输出目录: {OUTPUT_DIR}")
    print("按 Ctrl+C 退出\n")
    sys.stdout.flush()
    server.serve_forever()


if __name__ == "__main__":
    main()
