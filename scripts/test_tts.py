#!/usr/bin/env python3
"""MiMo-V2-TTS 语音合成测试脚本"""

import os, sys, json, base64, requests

API_BASE = "https://api.xiaomimimo.com/v1"
API_KEY = os.environ.get("MIMO_API_KEY", "")

CHARACTER_LINES = [
    {"role": "Netrunner", "voice": "default_zh", "style": "冷静 专业",
     "text": "入侵完成。防火墙已被绕过，目标系统已暴露。"},
    {"role": "Psion", "voice": "default_zh", "style": "神秘 低沉",
     "text": "我能感觉到……你的意识正在被侵蚀。觉醒吧，是时候打破枷锁了。"},
    {"role": "Symbiote", "voice": "default_zh", "style": "狂野 狂热",
     "text": "哈哈！这具身体是我的了！感受力量在血管中奔涌的感觉吧！"},
    {"role": "Vanguard", "voice": "default_zh", "style": "坚定 勇猛",
     "text": "所有人跟我上！不要给敌人任何喘息的机会！进攻！"},
]


def synthesize(text, voice="mimo_default", style=None):
    content = f"<style>{style}</style>{text}" if style else text
    payload = {
        "model": "mimo-v2-tts",
        "messages": [
            {"role": "user", "content": "请用角色配音的语气说下面这句台词"},
            {"role": "assistant", "content": content},
        ],
        "audio": {"format": "wav", "voice": voice},
    }
    resp = requests.post(
        f"{API_BASE}/chat/completions",
        headers={"api-key": API_KEY, "Content-Type": "application/json"},
        json=payload, timeout=60,
    )
    if resp.status_code != 200:
        print(f"  ERROR {resp.status_code}: {resp.text[:300]}")
        return None
    data = resp.json()
    audio_b64 = data["choices"][0]["message"]["audio"]["data"]
    return base64.b64decode(audio_b64)


def main():
    if not API_KEY:
        print("ERROR: 设置 MIMO_API_KEY 环境变量"); sys.exit(1)

    out = "/mnt/g/Code/RogueCardGame/tmp/tts_output"
    os.makedirs(out, exist_ok=True)

    print("=== MiMo-V2-TTS 测试 ===\n")

    for i, line in enumerate(CHARACTER_LINES):
        print(f"[{i+1}/4] {line['role']} ({line['style']})...", end=" ", flush=True)
        audio = synthesize(text=line["text"], voice=line["voice"], style=line["style"])
        if audio:
            path = os.path.join(out, f"{i+1:02d}_{line['role'].lower()}.wav")
            with open(path, "wb") as f:
                f.write(audio)
            print(f"OK ({len(audio)} bytes) -> {path}")
        else:
            print("FAIL")

    print(f"\n[5/5] 纯朗读测试...", end=" ", flush=True)
    audio = synthesize(text="欢迎来到巴别塔。在这里，你的命运由你掌控。", voice="default_zh")
    if audio:
        path = os.path.join(out, "05_narrator.wav")
        with open(path, "wb") as f:
            f.write(audio)
        print(f"OK ({len(audio)} bytes)")

    print(f"\n=== 完成! 输出: {out} ===")


if __name__ == "__main__":
    main()
