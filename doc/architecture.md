# 🏗️ 项目架构文档

## 技术栈

| 组件 | 选型 | 说明 |
|------|------|------|
| **引擎** | Godot 4.3+ (.NET) | 开源, 轻量, C# 支持 |
| **语言** | C# (.NET 8) | 强类型, 适合复杂逻辑 |
| **网络** | GodotSteam (Steam P2P) | Lobby + NAT 穿透 + Relay |
| **序列化** | System.Text.Json | 状态同步 / 存档 |
| **数据格式** | JSON | 卡牌 / 敌人 / 遗物 / 平衡数据 |
| **本地化** | CSV | 中文 / 英文 / 日文 |
| **测试** | NUnit | 核心逻辑单元测试 |
| **版本控制** | Git + Git LFS | 美术资源用 LFS |

## 项目结构

```
RogueCardGame/
├── project.godot              # Godot 项目配置
├── RogueCardGame.sln          # .NET 解决方案
├── RogueCardGame.csproj       # 项目文件 (Microsoft.NET.Sdk + GodotSharp 4.3.0)
├── global.json                # .NET SDK 版本固定 (8.0)
│
├── doc/                       # 📖 项目文档
│   ├── game-design.md         # 完整游戏设计文档
│   ├── architecture.md        # 本文件 — 架构参考
│   └── development-log.md     # 开发进度记录
│
├── src/                       # 💻 源代码
│   ├── Core/                  # 核心逻辑 (零 Godot 依赖, 可独立测试)
│   │   ├── Cards/             # 卡牌系统
│   │   ├── Combat/            # 战斗系统 (含前后排/仇恨/环境/AI)
│   │   ├── Characters/        # 角色与敌人
│   │   ├── Deck/              # 牌组管理
│   │   ├── Map/               # 地图生成
│   │   ├── Relics/            # 遗物系统
│   │   ├── Implants/          # 植入体系统
│   │   ├── Salvage/           # 入侵/改造系统
│   │   ├── Potions/           # 药水系统
│   │   ├── Environment/       # 动态战场环境
│   │   ├── Events/            # 随机事件
│   │   ├── Shop/              # 商店
│   │   ├── Progression/       # 元进度 / 攀升系统
│   │   ├── Run/               # 单局运行状态
│   │   ├── LocalizationManager.cs
│   │   └── SaveManager.cs
│   │
│   ├── Multiplayer/           # 🌐 多人网络层
│   │   ├── NetworkManager.cs  # P2P 通信管理
│   │   ├── LobbyManager.cs   # 房间创建/加入
│   │   ├── StateSync.cs       # 状态同步 (快照/Delta/校验)
│   │   ├── MultiplayerTurnManager.cs  # 多人回合管理
│   │   ├── SynergyLinkSystem.cs       # 协同链系统
│   │   ├── ReviveSystem.cs    # 倒下/复活机制
│   │   └── PvPManager.cs      # PvP 模式管理
│   │
│   └── UI/                    # 🎨 Godot UI 脚本
│       ├── CardUI.cs          # 卡牌 UI 组件
│       ├── HandDisplay.cs     # 手牌展示
│       ├── CombatHUD.cs       # 战斗 HUD
│       ├── MapUI.cs           # 地图界面
│       └── GameRunner.cs      # 游戏入口 / 场景管理
│
├── data/                      # 📊 数据驱动配置
│   ├── cards/                 # 卡牌数据 (4 职业 + 链接卡)
│   │   ├── vanguard_cards.json   # 先锋 15 张
│   │   ├── psion_cards.json      # 灵能者 15 张
│   │   ├── netrunner_cards.json  # 黑客 15 张
│   │   ├── symbiote_cards.json   # 共生体 15 张
│   │   └── link_cards.json       # 链接卡 14 张
│   ├── enemies/               # 敌人数据 (3 幕)
│   │   ├── act1_enemies.json
│   │   ├── act1_boss.json
│   │   ├── act2_enemies.json
│   │   ├── act2_boss.json
│   │   ├── act3_enemies.json
│   │   └── act3_boss.json
│   ├── relics/
│   │   └── team_relics.json      # 5 个团队遗物
│   ├── potions/
│   │   └── potions.json          # 12 种药水
│   ├── implants/
│   │   └── implants.json         # 9 个植入体
│   ├── events/
│   │   └── events.json           # 8 个随机事件
│   ├── pvp/
│   │   └── pvp_balance.json      # PvP 平衡层 + 预构筑牌组
│   ├── balance/
│   │   └── balance.json          # 全局平衡参数 + 多人缩放
│   └── localization/
│       └── strings.csv           # 三语翻译 (55+ 条目)
│
├── scenes/                    # 🎬 Godot 场景文件 (待创建)
├── assets/                    # 🖼️ 美术/音频资源 (Git LFS)
└── addons/                    # 🔌 Godot 插件 (GodotSteam 等)
```

## 核心设计模式

### 1. 数据驱动 (Data-Driven)

所有游戏内容 (卡牌、敌人、遗物、植入体、药水、事件) 通过 JSON 文件定义，代码只实现通用逻辑。

```
JSON 数据 → CardDatabase.LoadFromJson() → Card 实例
JSON 数据 → Enemy.LoadFromJson() → Enemy 实例
```

### 2. 命令模式 (Command Pattern)

卡牌效果使用 `ICardEffect` 接口，支持组合：

```csharp
public interface ICardEffect
{
    void Execute(CombatContext context, Character source, Character target);
}

// 8 种具体效果: Damage, Block, Draw, ApplyStatus,
//   Heal, GainEnergy, AreaDamage, HackProgress
// CompositeEffect: 组合多个效果为一张卡牌
```

### 3. 种子随机 (Seeded Random)

`SeededRandom` 类确保所有随机结果可确定性重现，关键用于：
- 多人同步验证
- 存档/读档一致性
- Bug 复现

### 4. 核心逻辑分离

`src/Core/` 中的所有类**零 Godot 依赖**，只使用标准 .NET 库。
- 优点：可独立进行 NUnit 单元测试
- 与 Godot 的桥接在 `src/UI/` 层完成

### 5. 事件总线 (Event Bus)

`EventBus` 提供全局事件发布/订阅，解耦各系统：

```csharp
EventBus.Instance.Subscribe<CardPlayedEvent>(OnCardPlayed);
EventBus.Instance.Publish(new CardPlayedEvent { ... });
```

### 6. 主机权威模型 (Host-Authoritative)

多人模式采用 P2P 架构，Host 为权威方：

```
Host (运算所有逻辑)
  ├── 接收客户端输入
  ├── 运算战斗结果
  ├── 广播状态快照/Delta
  └── 校验和 (Checksum) 验证一致性

Client (只发送输入, 接收状态)
  ├── 预测卡牌选择/站位切换
  ├── 状态插值 (动画平滑)
  └── 断线重连 (基于状态快照)
```

## 关键类职责

| 类 | 文件 | 职责 |
|----|------|------|
| `CombatManager` | Combat/CombatManager.cs | 战斗主循环: 回合管理、出牌结算、胜负判定 |
| `FormationSystem` | Combat/FormationSystem.cs | 前后排站位、位置修正 (近战-25%/远程+15%) |
| `AggroSystem` | Combat/AggroSystem.cs | 仇恨值追踪、嘲讽、目标选择 |
| `CardDatabase` | Cards/CardDatabase.cs | JSON 加载卡牌, 按职业/稀有度查询 |
| `CardEffectFactory` | Cards/CardEffect.cs | 从 JSON effect 数组构建 ICardEffect 组合 |
| `RunState` | Run/RunState.cs | 单局状态: 牌组/遗物/植入体/药水/地图/货币 |
| `MapSystem` | Map/MapSystem.cs | 程序化地图生成 (15 层, 分支路径) |
| `EnvironmentSystem` | Environment/EnvironmentSystem.cs | 9 种战场环境效果 (EMP/辐射/重力...) |
| `AdaptiveAI` | Combat/AdaptiveAI.cs | 玩家行为分析 + AI 权重偏移 (≤30%) |
| `ImplantSystem` | Implants/ImplantSystem.cs | 3 插槽植入体管理 (Neural/Somatic/Core) |
| `HackSystem` | Salvage/HackSystem.cs | 入侵进度追踪、控制触发 |
| `MetaProgress` | Progression/MetaProgress.cs | 跨局解锁、攀升系统 (20 级)、统计 |
| `NetworkManager` | Multiplayer/NetworkManager.cs | P2P 通信、消息路由、对等方管理 |
| `StateSync` | Multiplayer/StateSync.cs | 状态快照、Delta 更新、校验和验证 |
| `SynergyLinkSystem` | Multiplayer/SynergyLinkSystem.cs | 5 种链接类型 (攻击/防御/增幅/连锁/治疗) |
| `PvPManager` | Multiplayer/PvPManager.cs | 1v1/2v2 模式、独立 PvP 数值层 |
| `LocalizationManager` | Core/LocalizationManager.cs | CSV 三语 i18n (zh_cn/en/ja) |
| `SaveManager` | Core/SaveManager.cs | JSON 存档 + SHA256 校验 |

## 构建说明

### 环境要求
- .NET 8 SDK
- Godot 4.3+ (.NET 版)

### 命令行构建 (不启动 Godot)

```powershell
# 注意: 如果系统 MSBuildSDKsPath 指向旧版 SDK, 需先清除:
$env:MSBuildSDKsPath = $null
dotnet build
```

### 在 Godot 编辑器中构建

需将 `RogueCardGame.csproj` 中的 SDK 从 `Microsoft.NET.Sdk` 改回 `Godot.NET.Sdk/4.3.0`，并移除 GodotSharp NuGet 引用（详见 csproj 注释）。

### 当前状态

项目包含完整的游戏逻辑代码（编译 0 错误），但尚未创建 Godot 场景文件 (.tscn)。需要创建场景并挂载 UI 脚本才能在引擎中运行。

## 五大创新系统交互图

```
                    ┌─────────────────┐
                    │   前后排站位系统   │
                    │  (Formation)     │
                    └───────┬─────────┘
                            │
              影响目标选择/伤害修正
                            │
    ┌──────────┐    ┌───────▼─────────┐    ┌──────────────┐
    │ 动态战场  │───▶│    战斗系统       │◀───│  适应性 AI    │
    │环境系统   │    │ (CombatManager)  │    │ (AdaptiveAI) │
    └──────────┘    └───────┬─────────┘    └──────────────┘
                            │
              ┌─────────────┼─────────────┐
              │             │             │
      ┌───────▼──────┐ ┌───▼────┐ ┌──────▼───────┐
      │  协同链系统    │ │入侵系统│ │  植入体系统   │
      │(SynergyLink) │ │(Hack)  │ │  (Implant)   │
      └──────────────┘ └────────┘ └──────────────┘
              │                           │
              └─── 多人合作时深度配合 ──────┘
```
