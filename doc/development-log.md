# 📋 开发进度记录

## 项目概况

- **项目名称**: RogueCardGame
- **开始时间**: 2026-04
- **当前状态**: 核心逻辑完成, 待接入 Godot 场景

## 完成阶段

### ✅ 阶段一：核心原型 (Phase 1 - Core Prototype)

**提交记录**:
- `347150a` - Phase 1: Core prototype - project setup and combat system foundation
- `0fd2f57` - Phase 1 complete: Basic combat UI, GameRunner, and full combat flow

**完成内容**:
- Godot 项目搭建, C# 环境配置 (.NET 8)
- 战斗系统核心: CombatManager, TurnSystem, EnergySystem
- 前后排站位系统 (FormationSystem) + 仇恨机制 (AggroSystem)
- 卡牌效果系统: ICardEffect 接口 + 8 种具体效果 + CompositeEffect
- CardDatabase: JSON 数据加载, 按职业/稀有度查询
- 基础敌人 AI (含站位逻辑和意图循环)
- 基础 UI 脚本: CardUI, HandDisplay, CombatHUD, MapUI
- GameRunner 入口和 EventBus 事件总线
- 先锋 (Vanguard) 15 张基础卡牌 (JSON)
- 第一幕普通敌人 6 种 (JSON)

---

### ✅ 阶段二：单局循环 (Phase 2 - Run Loop)

**提交记录**:
- `161a924` - Phase 2: Single run loop systems

**完成内容**:
- MapSystem: 程序化地图生成 (15 层, 4-6 分支路径)
- ShopSystem: 卡牌/遗物/药水/移除服务
- RelicSystem: 遗物管理 + 触发时机 (OnBattleStart/OnCardPlayed/OnTurnEnd 等)
- ImplantSystem: 3 插槽植入体 (Neural/Somatic/Core), 9 个植入体定义
- HackSystem + SalvageSystem: 入侵进度追踪 + 零件改造系统
- EventSystem: 8 个随机事件 (含分支选择)
- PotionSystem: 12 种药水 (攻击/防御/功能性)
- EnvironmentSystem: 9 种战场环境效果 (EMP/辐射/重力异常...)
- AdaptiveAI: 玩家行为档案 + AI 权重偏移 (上限 30%)
- RunState: 整合所有子系统的单局运行状态
- 第一幕 Boss: 赛博巨龙 (HP 150, 4 种意图)

---

### ✅ 阶段三：多人合作 (Phase 3 - Multiplayer Co-op)

**提交记录**:
- `80b8949` - Phase 3: Multiplayer systems and synergy link

**完成内容**:
- NetworkManager: 主机权威 P2P, 消息路由, 对等方管理
- LobbyManager: 房间创建/加入, 职业选择, 准备状态
- StateSync: 战斗快照, Delta 更新, 校验和验证, 断线重连
- MultiplayerTurnManager: 同时规划 → 速度序列结算
- SynergyLinkSystem: 5 种链接类型 (攻击/防御/增幅/连锁/治疗)
- ReviveSystem: 濒死状态 (3 回合) + 队友治疗复活
- PvPManager: 1v1/2v2 模式框架
- 链接卡数据: 14 张 (每职业 3 张 + 中立 2 张)
- 团队遗物: 5 个 (量子纠缠器/共享能量矩阵等)

---

### ✅ 阶段四：内容扩充 (Phase 4 - Content Expansion)

**提交记录**:
- `a6dc475` - Phase 4: Content expansion - all classes, 3 acts, meta-progression

**完成内容**:
- 灵能者 (Psion) 15 张卡牌: 共鸣叠层机制
- 黑客 (Netrunner) 15 张卡牌: 协议栈 + 入侵
- 共生体 (Symbiote) 15 张卡牌: 侵蚀/自残输出
- 第二幕完整内容: 6 种敌人 + Boss (虚空主教, HP 250)
- 第三幕完整内容: 6 种敌人 + 最终 Boss (巴别核心, 4 阶段, HP 500)
- MetaProgress: 跨局解锁系统, 攀升难度 (20 级), 全局统计
- 攀升修改器: 20 级递增难度 (敌人 HP+/伤害+/精英增强/Boss 强化...)

---

### ✅ 阶段五：PvP 模式 (Phase 5 - Competitive)

**提交记录**:
- `0a19243` - Phase 5-6: PvP balance, localization, save system, polish systems

**完成内容**:
- PvP 独立数值层: 伤害 ×0.75, 治疗 ×0.8
- Hack → Disrupt 转换 (PvP 中禁用对手下一张牌而非控制)
- 4 套预构筑牌组 (每职业 1 套, 用于快速 PvP)
- 卡牌 PvP 覆盖规则 (individual card overrides)
- 多人缩放参数 (2-4 人敌人 HP/数量调整)

---

### ✅ 阶段六：打磨与发布 (Phase 6 - Polish)

**提交记录**: (与阶段五同一提交)

**完成内容**:
- LocalizationManager: CSV 三语 i18n (zh_cn / en / ja)
- strings.csv: 55+ 翻译条目 (UI/战斗/状态/地图)
- L.Get() / L.T() / L.F() 静态快捷方法
- SaveManager: JSON 存档 + SHA256 校验和验证
- GameSettings: 音频/视频/游戏设置持久化
- SaveData: 元进度 + 进行中的局数据

---

## 项目统计

| 统计项 | 数据 |
|--------|------|
| 总 C# 源文件 | 32 |
| 总 JSON/CSV 数据文件 | 17 |
| 总项目配置文件 | 6 |
| Git 提交数 | 6 |
| 编译错误 | 0 |
| 编译警告 | 7 (未使用的字段/事件, 预留给 UI 集成) |

## 后续待完成

### 优先级 P0 (核心)
- [ ] 创建 Godot 场景文件 (.tscn) — 让游戏可以实际运行
  - 主菜单场景
  - 战斗场景 (含前后排 UI)
  - 地图场景
  - 商店/休息点/事件场景
  - 多人大厅场景
- [ ] 将 csproj SDK 切回 Godot.NET.Sdk 以支持编辑器
- [ ] 集成 GodotSteam 插件实现真实 P2P

### 优先级 P1 (重要)
- [ ] AudioManager 音频系统
- [ ] NUnit 单元测试 (核心逻辑)
- [ ] 占位美术资源 (卡牌/角色/敌人)
- [ ] 平衡性调试框架

### 优先级 P2 (增强)
- [ ] Steam 成就集成
- [ ] PvP Ban/Pick 系统
- [ ] PvP 匹配/段位系统
- [ ] Steam 云存档
- [ ] 正式美术资源制作
- [ ] BGM / 音效
- [ ] Beta 测试

## 已知技术问题

1. **MSBuildSDKsPath 环境变量冲突**: 系统级设为 SDK 5.0 路径, 需在每个新 shell 中清除才能用 .NET 8 构建
2. **Godot 源代码生成器**: `[Signal]` 生成的 `SignalName.*` 常量在编辑器外不可用, 已用 `event Action<T>` 替代
3. **无 Godot 场景文件**: 所有逻辑为纯 C#, 需创建 .tscn 场景挂载脚本才能运行游戏
