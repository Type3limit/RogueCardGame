# 开发进度记录

## 项目概况

- **项目名称**: RogueCardGame
- **开始时间**: 2026-04
- **设计版本**: v3
- **引擎**: Godot 4.6 + C# (.NET 8)
- **核心系统**: 站位 + 植入体（深度咬合）

## 当前阶段: Phase 3 — Roguelike 循环完整性

---

## Core 架构重构（2026-04-17，完成 P0–P4 + P7，P5 部分，P6 基础）

**动因**：定位核心层为 `CardEffectFactory + ActionManager + AbstractPower` 三叉架构，但三者扩展点互不相通，导致多条"描述-实现漂移"。

**完成**：
- P0：6 个破损 Power 全部修复 — `HealOnKillPower.OnKill` 落地；`ResonanceAmplifyPower` 消费时自移除（`RemovePowerDelayedAction`）；`WarMachinePower` 每层 +1 Block、每 3 层抽 1 张；`FrontlineCommanderPower` 接入 `ModifyOverchargeConsumeDamage` 管线；`ParasiticBondPower.OnTakeDamage` 按伤害 % 回血给 Source；`MindSovereignPower` 改用真正的 `MarkPower`（不再误借 Vulnerable）
- P1：新增 `CombatEventDispatcher` 作为战斗级触发总线（附加层，与现有 Power 钩子并行）
- P2：`CardEffectContext.DrawCardCallback` / `AddToDiscardCallback` 全面下线，所有卡牌效果统一走 `ActionManager`；引入 `ScryEffect.TryDraw` 内部助手
- P3：`src/Core/Combat/Expr.cs` record 条件 DSL，`StringCondition` 兜底兼容所有旧字符串条件
- P4：`src/Core/Combat/TargetSelector.cs`（Self/AllEnemies/AllAllies/RandomEnemy），`SelfTargetWrapper` 升级为可配置
- P5（部分）：带行为的 core implant（overchargeToStrength / turnSelfDamage / overloadCircuit 等）暂保留在 CombatManager 条件块，纯数值 Pull 查询（`GetTotalBonus`）保留不动。完整迁移需更多测试覆盖
- P6：新增 `tests/RogueCardGame.Tests/CorePowerRegressionTests.cs`（11 条新回归测试）。文本一致性自动校验仍待做
- P7：`src/Core` 全量去 Godot —— 新增 `src/Core/Utils/EffectLog.cs`（`IEffectLogger`）；Godot 侧 autoload `scripts/autoload/GodotEffectLogger.cs` 注入 logger；核心层不再 `using Godot`

**附带补丁**：
- `AbstractPower.ModifyOverchargeConsumeDamage` 新虚钩子 + `PowerManager` 聚合
- `PowerManager.ApplyPower(power, owner, source?)` 三参签名，贯穿 `ApplyPowerAction`，供 `ParasiticBond` 追踪来源
- `CombatManager.TryPlayCard` 走 `player.Powers.ModifyCardCost`，使 `MindSovereign`/`CompileAccel` 真正影响费用
- `CombatManager.Events` + `CombatManager.Log` 暴露

**验证**：`dotnet test` = **29/29 绿**（原 18 + 新增 11）；`src/**/*.cs` 下无 `Godot.GD` 调用。

**详见**：[doc/核心层重构规划.md](核心层重构规划.md) §5 实施状态表。`doc/refactorCore.md` 的战术清单已全部吸纳并可删除。

---

## Phase 1 — 让游戏能跑一局完整流程 ✅ 完成

### 已完成
- [x] 核心逻辑: 卡牌/战斗/敌人AI/牌组/地图/商店/事件/植入体/药水/环境/进度
- [x] 数据文件: 4职业卡牌/3幕敌人/植入体/药水/事件/平衡参数
- [x] 文档清理: 移除所有 v2 废弃设计内容
- [x] 切换 csproj 到 Godot.NET.Sdk/4.3.0
- [x] 职业系统 JSON 驱动化 (ClassDatabase + data/classes/*.json)
- [x] Autoload 系统 (GameManager/AudioManager/SceneManager)
- [x] 全部 Godot 场景: Main/MainMenu/Map/Combat/Shop/Event/Rest/Reward/GameOver/Victory/Settings
- [x] 战斗场景: 手牌展示/敌人面板/意图/伤害飘字/阵型切换/卡牌瞄准
- [x] 地图场景: 杀戮尖塔风格分叉路径/节点类型着色/路径连线
- [x] BalanceConfig: 数值外部化到 balance.json
- [x] FormationSystem/AggroSystem 常量从 balance.json 读取
- [x] RunState 使用 ClassDatabase 创建角色
- [x] 植入体系统: 3→2 槽位 (Neural + Core)
- [x] 移除废弃系统: 多人模块/PvP/旧UI/废弃房间类型
- [x] 赛博朋克 UI 主题: cyberpunk_theme.tres
- [x] 占位视觉资源: SVG 图标/卡牌模板/角色剪影/敌人/地图节点
- [x] 占位音频系统: PlaceholderAudioGenerator 自动生成静音 WAV
- [x] 完整游戏循环: 主菜单→职业选择→地图→战斗/事件/商店/休息→奖励→Boss→胜利/失败
- [x] 设置场景: 音量/全屏控制
- [x] BossEnemyAI / EliteEnemyAI 重写（组合模式，非继承）
- [x] 6 个 Core 植入体 effect 接入（resonanceDecayHalf / resonanceDrawBonus / protocolStackSize / hackSpeedBonus / erosionSelfDamageHalf / erosionBonusDamage）
- [x] CardRarity.Legendary 枚举值补齐
- [x] EventChoiceEffect 枚举值补齐（HealPercent / LoseGoldAll / LoseHpPercent）
- [x] CardEffectData.Multiplier 改为 double（支持 1.5x）
- [x] EventChoiceOutcome.Value 改为 double
- [x] JSON 枚举统一 camelCase（rarity / class / type / range / target）
- [x] Build: 0 error, 0 warning | Test: 18/18 pass

---

## Phase 2 — 卡牌内容扩展 ✅ 完成

### 卡牌数据

| 职业 | 数量 | 目标 | 状态 |
|------|------|------|------|
| Vanguard（先锋） | 35 | 35 | ✅ |
| Psion（灵能者） | 37 | 35 | ✅ |
| Netrunner（黑客） | 35 | 35 | ✅ |
| Symbiote（共生体） | 36 | 35 | ✅ |
| 位置反应牌（通用） | 18 | 15-20 | ✅ |

### 植入体系统

- [x] 6 个神经植入体：neural_optimizer / neural_overclocker / overload_circuit / pulse_processor / precognition_module / battlefield_interface
- [x] 5 个核心植入体：vanguard_berserker_core / vanguard_reflector_core / psion_resonance_core / netrunner_protocol_core / symbiote_control_core
- [x] 设计原则合规：4 个神经植入体已去除数值型叠加（保留纯规则变化）
- [x] 运行时 hook：energy / drawPerTurn / resonanceDrawBonus / turnStartBlock / precognitionModule（scry） / pulseProcessor / overchargeToStrength / overchargeThorns / turnSelfDamage / overloadCircuit
- [ ] battlefieldInterface 运行时 hook（数据存在，尚未实现 — 不阻塞 Phase 3）

### 事件系统

- [x] 11 个事件，分支选择 + 概率后果 + 条件判断
- [x] 效果类型：gainGold / loseGold / gainHp / loseHp / gainMaxHp / loseMaxHp / gainPotion / losePotion / upgradeCard / removeCard / gainImplant / gainCard / healPercent / loseHpPercent / loseGoldAll

### 战斗系统增强

- [x] Scry 机制：precognitionModule 每 2 回合触发，OnScryTriggered 事件 + CompleteScry 回调
- [x] DeckManager 新增：DrawPileCount / TakeFromDrawPile / AddToDrawPileBottom
- [x] pulseProcessor：出牌后立即抽 1 牌（如果手牌未满）

---

## Phase 3 — Roguelike 循环完整性 进行中

### 3.1 地图系统 ✅
- [x] 分叉路径（MapGenerator 2-4 节点/行）
- [x] 节点类型：Combat / EliteCombat / RestSite / Shop / Event / Boss / Treasure / Start / Victory
- [x] Boss 节点在每段末尾
- [x] 场景路由：MapScene 根据节点类型跳转

### 3.2 敌人数据 ✅
- [x] Act 1：7 普通 + 1 精英（drone_carrier）+ 1 Boss（sentinel_ai，3 阶段）
- [x] Act 2：6 普通 + 4 精英 + 1 Boss（data_overlord，3 阶段）
- [x] Act 3：4 普通 + 2 精英 + 1 Boss（babel_core，4 阶段）
- [x] 多阶段 Boss AI（BossEnemyAI 组合模式）
- [x] 精英 AI（EliteEnemyAI，每回合行动两次，50% HP 狂暴）

### 3.3 商店系统 ✅
- [x] ShopManager：5 卡 + 2 药水 + 1 植入体 + 1 移除服务
- [x] 价格随 Act 递增（1.0x → 1.25x → 1.5x）
- [x] ShopScene：购买 / 可用性检查 / 卡牌移除叠加层

### 3.4 奖励系统 ✅
- [x] 战斗：3 选 1 卡牌 + 金币 + 药水概率掉落
- [x] 精英：3 选 1 卡 + 2 选 1 植入体
- [x] Boss：稀有/传奇植入体 + 稀有卡牌选择

### 3.5 事件系统 ✅
- [x] 11 个事件，覆盖 act 限制 / 一次性事件 / 条件分支

### 3.6 存档系统 ✅
- [x] SaveManager：SHA-256 校验，meta + run + settings 分离存储
- [x] SaveData / GameSettings 数据结构完整

### 3.7 待验证
- [ ] 场景间串联端到端测试（地图 → 战斗 → 奖励 → 地图）
- [ ] Godot 编辑器首次运行调试
- [ ] 实际游戏平衡性体验（数值是否需要调整）

---

## Phase 4 — 差异化与打磨（待开始）

- [ ] 站位系统深化（敌人也有站位、阵型联动）
- [ ] 连击感 / 节奏感（动画延迟、多段伤害飘字、击杀停顿）
- [ ] 第二职业打通（Vanguard 跑通后扩展 Psion）
- [ ] 成就 / 解锁系统
- [ ] 真实音频/美术资源替换

---

## 技术债务

| 问题 | 位置 | 状态 |
|------|------|------|
| battlefieldInterface 无运行时 hook | CombatManager.cs | 待实现 |
| _postSwitchDiscountReady 字段 write-only | CombatManager.cs | 跟随 battlefieldInterface 一并处理 |
| Scry 交互为异步但 StartPlayerPhase 同步执行 | CombatManager.cs | 需 Scene 层配合 |
| LifestealEffect 治疗量估算不准 | CardEffect.cs | 低优先级 |
| 美术全是占位 SVG | resources/textures/ | Phase 4 |
| 音频全是静音 WAV | resources/audio/ | Phase 4 |

---

## 废弃/已删除

- 多人模块 (src/Multiplayer/) — 整个目录已删除
- PvP 系统 — 已删除
- 旧 UI 文件 (src/UI/) — 全部删除，由场景脚本替代
- Program.cs 控制台入口 — 已删除
- 协同链/入侵改造/适应性AI/遗物系统 — 已删除
- ModStation/DataTerminal 房间类型 — 已移除
- 躯体(Somatic)植入体槽位 — 已移除
