# RogueCardGame - 运行指南

## 前置要求

1. **.NET 8 SDK** — 已配置（`global.json` 固定 8.0.100）
2. **Godot 4.6.2** — 已内置于 `gdot/` 目录，无需额外下载

## 快速启动

### 方法一：通过 Godot 编辑器运行（推荐）

```powershell
# 在项目根目录打开 Godot 编辑器
.\gdot\Godot_v4.6.2-stable_win64.exe --path .
```

编辑器打开后：
1. 等待底部状态栏显示 **"导入完成"** / **"Import done"**（首次打开需要导入所有资源）
2. 如果提示 C# 编译错误，点击 **MSBuild → 重新编译** 或按 `Alt+B`
3. 点击右上角 **▶（运行项目）** 或按 `F5` 启动游戏

### 方法二：直接运行（跳过编辑器）

```powershell
# 先确保编译通过
dotnet build

# 直接运行主场景
.\gdot\Godot_v4.6.2-stable_win64.exe --path . --run
```

> ⚠️ 首次运行建议使用方法一，让 Godot 先导入所有资源（SVG、WAV 等）。

### 方法三：使用控制台版本（显示日志输出）

```powershell
.\gdot\Godot_v4.6.2-stable_win64_console.exe --path . --run
```

控制台版本会在终端中显示 `GD.Print()` 输出和错误信息，方便调试。

## 项目结构概览

```
RogueCardGame/
├── project.godot          # Godot 项目配置（入口场景、自动加载等）
├── RogueCardGame.csproj   # C# 项目文件（Godot.NET.Sdk 4.3.0）
├── global.json            # .NET SDK 版本锁定
│
├── scenes/                # Godot 场景文件 (.tscn + .cs)
│   ├── main/              # Main - 入口场景管理器
│   ├── menus/             # MainMenu, GameOver, Victory
│   ├── combat/            # Combat - 战斗场景
│   ├── map/               # Map - 地图场景
│   ├── shop/              # Shop - 商店场景
│   ├── event/             # Event - 事件场景
│   ├── rest/              # Rest - 休息场景
│   ├── reward/            # Reward - 奖励场景
│   ├── settings/          # Settings - 设置场景
│   └── gameover/          # (空 - 使用 menus/GameOver)
│
├── scripts/               # Godot 全局脚本
│   ├── autoload/          # 自动加载单例
│   │   ├── GameManager.cs   # 游戏状态管理
│   │   ├── AudioManager.cs  # 音频管理（BGM + SFX）
│   │   └── SceneManager.cs  # 场景切换（带淡入淡出）
│   └── tools/             # 工具脚本
│
├── src/Core/              # 核心游戏逻辑（纯 C#）
│   ├── Cards/             # 卡牌系统
│   ├── Characters/        # 角色 / 敌人 / AI
│   ├── Combat/            # 战斗管理、阵型、仇恨
│   ├── Deck/              # 牌组管理
│   ├── Map/               # 地图生成
│   ├── Shop/              # 商店
│   ├── Events/            # 事件系统
│   ├── Relics/            # 遗物系统
│   ├── Potions/           # 药水系统
│   ├── Implants/          # 植入物系统
│   ├── Run/               # 单局状态
│   ├── Progression/       # Meta 进度
│   └── Environment/       # 环境效果
│
├── data/                  # JSON 数据文件
│   ├── balance/           # 平衡配置
│   ├── cards/             # 卡牌定义（6 个职业文件）
│   ├── enemies/           # 敌人定义（3 层 + Boss）
│   ├── events/            # 事件定义
│   ├── relics/            # 遗物定义
│   ├── potions/           # 药水定义
│   └── implants/          # 植入物定义
│
├── resources/             # 游戏资源
│   ├── audio/
│   │   ├── bgm/          # 8 首 BGM（WAV 格式）
│   │   └── sfx/          # 14 个 SFX（WAV 格式）
│   ├── textures/
│   │   ├── backgrounds/   # 场景背景 SVG
│   │   ├── cards/         # 卡牌模板 SVG
│   │   ├── characters/    # 角色立绘 SVG
│   │   ├── enemies/       # 敌人图像 SVG
│   │   ├── effects/       # 意图图标 SVG
│   │   ├── map/           # 地图节点 SVG
│   │   └── ui/            # UI 图标 SVG
│   └── themes/            # Godot 主题
│
└── doc/                   # 设计文档
```

## 游戏流程

```
主菜单 → 选择职业 → 地图
                       ↓
              ┌──→ 战斗 → 奖励 ──┐
              │                    │
              ├──→ 精英 → 奖励 ──┤
              │                    │
     地图 ←──├──→ 商店 ─────────┤
              │                    │
              ├──→ 事件 ─────────┤
              │                    │
              └──→ 休息 ─────────┘
                       ↓
              Boss 战 → 下一层 或 胜利
```

## 四个可选职业

| 职业 | 颜色 | 特点 |
|------|------|------|
| **先锋 (Vanguard)** | 🟠 橙色 | 前排坦克，高护甲，嘲讽 |
| **黑客 (Netrunner)** | 🔵 青色 | 数据黑入，漏洞利用，控制 |
| **灵能者 (Psion)** | 🟣 紫色 | 灵能攻击，意念操控 |
| **共生体 (Symbiote)** | 🟢 绿色 | 有机/科技融合，自愈，适应 |

## 音频资源

所有音频由 Python 合成器生成（`generate_audio.py`），风格为赛博朋克合成器音效：

- **BGM (8首)**：主菜单氛围、地图探索、战斗节拍、Boss 激战、商店放松、事件神秘、胜利凯旋、失败哀伤
- **SFX (14个)**：出牌、选牌、抽牌、弃牌、攻击命中、获得护甲、治疗、状态施加、按钮点击/悬停、回合结束、敌人攻击、获得金币、地图节点选择

如需重新生成音频：`python generate_audio.py`
如需重新生成 SVG：`python generate_svgs.py`

## 常见问题

**Q: 首次打开编辑器时报错？**
A: Godot 需要先导入资源。等待导入完成后，在编辑器底部点击 "Build" 编译 C# 项目。

**Q: 看不到背景图？**
A: SVG 文件需要 Godot 导入后才能显示。首次打开编辑器会自动处理。

**Q: 音频听不到？**
A: AudioManager 优先加载 `.ogg`，其次 `.wav`。当前提供的是 `.wav` 格式，Godot 会自动识别。

**Q: 如何修改平衡数据？**
A: 编辑 `data/balance/balance.json`，无需重新编译。卡牌/敌人/事件等 JSON 文件同理。
