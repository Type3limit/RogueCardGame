# RogueCardGame 开发路线图

> 版本：v3 设计（赛博朋克「巴别塔」，单人 Roguelike 卡牌）
> 引擎：Godot 4.6 + C# (.NET 8)
> 最后更新：2026-04

---

## 当前状态快照

> 最后更新：2026-04-17 | Build: 0 error, warnings only | Test: **29/29 pass** | 核心层 P0-P7 重构已落地

### 已完成的核心系统

| 系统 | 文件 | 状态 |
|------|------|------|
| 战斗管理 | `CombatManager.cs` | ✅ 完整 |
| Action Queue | `ActionManager.cs` + `CommonActions.cs` | ✅ STS 同级 |
| 站位系统 | `FormationSystem.cs` | ✅ 完整 |
| 目标系统 | `TargetingSystem.cs` | ✅ 完整 |
| 回合状态机 | `TurnSystem.cs` | ✅ 完整 |
| Powers 系统 | `CommonPowers.cs` | ✅ 完整；2026-04-17 补 MarkPower/RootkitPower/CompileAccelPower/MeleeStrengthPower；HealOnKill/ResonanceAmplify/WarMachine/FrontlineCommander/ParasiticBond/MindSovereign 全部修复 |
| 位置反应型卡牌 | `RowConditionalEffect` / `CardEffectFactory` | ✅ 完整 |
| 卡牌升级 A/B 分支 | `Card.cs` | ✅ 完整 |
| 原型核心系统 | `PrototypeCoreSystem.cs` | ✅ 完整 |
| 植入体运行时 | `CombatManager.cs` | ✅ 11/12 hook 已接入 |
| 敌人 AI | `EnemyAI.cs` / `BossEnemyAI.cs` / `EliteEnemyAI.cs` | ✅ 组合模式，多阶段 |
| 仇恨系统 | `AggroSystem.cs` | ✅ 完整 |
| 卡牌数量 | 4 职业 35±2 张 + 18 张位置反应牌 = 161 张 | ✅ Phase 2 达标 |
| 植入体 | 6 神经 + 5 核心 = 11 个 | ✅ 完整 |
| 事件 | 11 个事件，分支选择 + 条件判断 | ✅ 完整 |
| 敌人数据 | 3 Act 共 17 普通 + 7 精英 + 3 Boss | ✅ 完整 |
| Save 系统 | `SaveManager.cs` | ✅ SHA-256 校验，meta+run+settings |
| 地图系统 | `MapSystem.cs` / `MapScene.cs` | ✅ 分叉路径，节点着色，路由 |
| 商店系统 | `ShopSystem.cs` / `ShopScene.cs` | ✅ 买卡/植入体/药水/移除 |
| 奖励系统 | `RewardScene.cs` | ✅ 3选1卡/精英植入体/Boss稀有 |
| UI / 场景层 | 场景脚本 | ✅ 骨架完整，待 Godot 实机调试 |
| 美术 / 音频 | — | ❌ 全是占位资源 |

### 已知问题

| 问题 | 位置 | 优先级 |
|------|------|--------|
| `battlefieldInterface` 植入体无运行时 hook | `CombatManager.cs` | Phase 3 |
| `LifestealEffect` 治疗量估算不准 | `CardEffect.cs` | 低优先级 |
| Scry 交互异步但 StartPlayerPhase 同步执行 | `CombatManager.cs` | 需 Scene 层 |
| `_postSwitchDiscountReady` 字段 write-only | `CombatManager.cs` | 跟随 battlefieldInterface |
| P5 残留：带行为的 core implant 仍硬编码在 `CombatManager` 条件块 | `CombatManager.cs` | 见核心层重构规划 §5 |
| 美术/音频全占位 | `resources/` | Phase 4 |

### 核心层重构（2026-04-17）

- [x] **P0** 6 个破损 Power 全部修复 + 4 个 fallback-only Effect 统一 ActionManager
- [x] **P1** `CombatEventDispatcher` 战斗级触发总线（附加层）
- [x] **P2** `DrawCardCallback` / `AddToDiscardCallback` 完全下线，效果统一走 ActionManager
- [x] **P3** `Expr` record 条件 DSL（兼容旧字符串）
- [x] **P4** `TargetSelector` 类族 + 工厂 per-effect 目标覆盖
- [x] **P7** Core 去 Godot（`IEffectLogger` + autoload）
- ⚠️ **P5** 部分：纯数值 `GetTotalBonus` 保留；带行为的 core implant 迁移为下阶段增量工作
- [x] **P6** 新增 11 条 Power 回归测试（`CorePowerRegressionTests.cs`）

详见 [doc/核心层重构规划.md](核心层重构规划.md) §5。

---

## Phase 1 — 让游戏能跑一局完整流程 ✅ 完成

**目标：能从主菜单开始，打完一场战斗，进地图，再打一场，游戏结束有结算。**

### 已完成

- [x] 6 个 Core 植入体 effect 全部接入
- [x] BossEnemyAI / EliteEnemyAI 重写为组合模式
- [x] CardRarity.Legendary 枚举补齐
- [x] EventChoiceEffect 枚举补齐（HealPercent / LoseGoldAll / LoseHpPercent）
- [x] CardEffectData.Multiplier / EventChoiceOutcome.Value 改为 double
- [x] JSON 枚举统一 camelCase
- [x] Save 系统完整（SHA-256 校验，meta+run+settings 分离）
- [x] 地图系统完整（分叉路径，节点着色，场景路由）
- [x] Build: 0 error, 0 warning | Test: 18/18 pass

---

## Phase 2 — 卡牌内容扩展 ✅ 完成

**目标：每个职业达到 30~40 张牌，且每张都有明确的站位策略意图。**

### 已完成

| 职业 | 数量 | 目标 | 状态 |
|------|------|------|------|
| Vanguard（先锋） | 35 | 35 | ✅ |
| Psion（灵能者） | 37 | 35 | ✅ |
| Netrunner（黑客） | 35 | 35 | ✅ |
| Symbiote（共生体） | 36 | 35 | ✅ |
| 位置反应牌（通用） | 18 | 15-20 | ✅ |

- [x] 6 个神经植入体 + 5 个核心植入体
- [x] 11 个事件，分支选择 + 条件判断
- [x] 4 个神经植入体已去除数值型叠加（overload_circuit / pulse_processor / precognition_module）
- [x] Scry 运行时 hook（precognitionModule）+ DeckManager 扩展（DrawPileCount / TakeFromDrawPile / AddToDrawPileBottom）
- [x] pulseProcessor 运行时 hook（出牌后抽 1）
- [x] `battlefieldInterface` hook 待实现（不阻塞 Phase 3）

---

## Phase 3 — Roguelike 循环完整性（当前）

**目标：循环感受接近成品，有运气成分，有构筑深度，有回放动力。**

### 3.1 地图质量 ✅ 数据就绪

- [x] 地图分叉路径：MapGenerator 2-4 节点/行，多路线选择
- [x] 节点类型：Combat / EliteCombat / RestSite / Shop / Event / Boss / Treasure
- [x] Boss 节点：每段 Act 末尾，3 Act 各有专属 Boss

### 3.2 精英/Boss 敌人 ✅ 数据就绪

- [x] Act 1：1 精英（drone_carrier，召唤无人机）+ 1 Boss（sentinel_ai，3 阶段）
- [x] Act 2：4 精英（cyber_guardian / emp_overlord / nano_swarm_queen / overclocked_enforcer）+ 1 Boss（data_overlord，3 阶段）
- [x] Act 3：2 精英（gravity_golem / singularity_core）+ 1 Boss（babel_core，4 阶段）
- [x] Boss AI：组合模式，多阶段意图循环，阶段切换触发特殊行为
- [x] 精英 AI：每回合行动两次，50% HP 狂暴

### 3.3 事件系统 ✅

- [x] 11 个事件，分支选择 + 概率后果 + 条件判断
- [x] 覆盖 act 限制 / 一次性事件 / gold/hp/implant 多种奖惩

### 3.4 商店系统 ✅

- [x] 5 卡 + 2 药水 + 1 植入体 + 1 卡牌移除服务
- [x] 价格随 Act 递增（1.0x → 1.25x → 1.5x）
- [x] ShopScene：购买 / 可用性检查 / 移除叠加层

### 3.5 奖励节点 ✅

- [x] 战斗胜利：3 选 1 卡牌 + 金币 + 药水概率
- [x] 精英胜利：3 选 1 卡 + 2 选 1 植入体
- [x] Boss 胜利：稀有/传奇植入体 + 稀有卡牌

### 3.6 存档系统 ✅

- [x] SaveManager：SHA-256 校验
- [x] meta（进度解锁）+ run（当前局）+ settings（设置）分离存储

### 3.7 待验证（Phase 3 剩余工作）

- [ ] 场景间串联端到端测试（地图 → 战斗 → 奖励 → 地图循环）
- [ ] Godot 编辑器首次运行调试（F5 实机验证）
- [ ] 实际游戏平衡性体验（数值是否需要调整）
- [ ] battlefieldInterface 植入体运行时 hook 实现

**Phase 3 完成标准：** 可以完整打一个 Act（15~20 个节点），有运气体验，有"这次 run 没拿到想要的牌"的感受。

---

## Phase 4 — 差异化与打磨

**目标：站位系统成为真正的核心差异点，不是其他卡牌游戏有的系统加了个站位而已。**

### 4.1 站位系统深化

- [ ] **敌人也有站位**：部分近战敌人必须打前排目标，逼玩家决策"我要不要去前排硬扛"
- [ ] **阵型联动**：多个敌人时，"把某个敌人推到后排 → 它无法攻击 → 消耗一回合"变成可行策略
- [ ] **位置改变的反馈**：UI 上前/后排分界线要非常清晰，换位动画要有感

### 4.2 连击感 / 节奏感

- [ ] 卡牌打出有小延迟（ActionType.Fast/Medium 对应实际动画时间）
- [ ] 多段伤害（MultiHitDamageAction）有逐次数字飘出
- [ ] 击杀时有停顿 + 特效

### 4.3 第二职业打通

- [ ] 先把 Vanguard 完整跑通（Phase 1-3），再扩展第二职业 Psion
- [ ] 两个职业的构筑感受要有明显不同（Vanguard 冲前排，Psion 守后排）

### 4.4 成就 / 解锁系统

- [ ] 每个职业有 3 个挑战性成就（如"本局不使用任何换位"）
- [ ] 解锁新的核心植入体 / 特殊卡牌

**Phase 4 完成标准：** 外部玩家能玩 30 分钟，清楚感受到"这个游戏的核心是站位决策"。

---

## 技术债务（随时修，不阻塞进度）

| 问题 | 位置 | 说明 |
|------|------|------|
| `OnDamageDealt` / `OnBlockGained` 事件未使用 | `CombatManager.cs` | 留给 UI 层用，暂时 warning 可接受 |
| `DeckManager.DrawCardCallback` 残留 | `CardEffectContext` | fallback 用途，不影响正确性 |
| `ScryEffect` 简化为抽牌 | `CardEffect.cs` | 需要 modal UI 才能实现完整版 |
| `LifestealEffect` 估算不准 | `CardEffect.cs` | Phase 1 修复，需要 context 回写伤害量 |
| 美术全是占位 SVG | `assets/` | Phase 4 再处理 |
| 音频全是静音 WAV | `assets/audio/` | Phase 4 再处理 |

---

## 设计约束备忘（勿违反）

- **植入体改变规则，不改变数值** — 数值型效果（maxHp/drawPerTurn/+能量）是临时植入体或事件奖励，不是长期方向
- **站位是成本，不是免费选项** — 换位消耗 1 能量；后排不能打近战
- **后排远程奖励限一次** — 每回合第一张远程牌 +20%，防止"永远待后排"最优解
- **植入体只有 2 槽** — Neural（出牌规则）+ Core（职业核心机制）
- **4 职业，单人优先** — 不要为多人设计妥协单人体验
- **Action Queue 优先** — 所有战斗效果必须通过 ActionManager 排队，不能直接修改状态
