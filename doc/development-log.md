# 开发进度记录

## 项目概况

- **项目名称**: RogueCardGame
- **开始时间**: 2026-04
- **设计版本**: v3
- **引擎**: Godot 4.6 + C# (.NET 8)
- **核心系统**: 站位 + 植入体（深度咬合）

## 当前阶段: Phase 4 — 差异化与打磨

### 2026-05-10 Phase 4 美术资源：四职业日系二次元主立绘首批接入

- 根据 `doc/世界观.md`、`doc/角色.md` 与四职业设定文档生成首批职业主视觉立绘，并按用户反馈将方向校正为日本二次元动漫 / 赛璐璐卡牌手游风格：
  - `resources/textures/characters/vanguard.png`
  - `resources/textures/characters/psion.png`
  - `resources/textures/characters/netrunner.png`
  - `resources/textures/characters/symbiote.png`
  - 原始四职业图集保留在 `resources/textures/characters/source/phase4_class_lineup.png`
- 接入位置：
  - `CyberCardFactory.GetClassPortraitPath()` 改为使用 PNG 主立绘
  - Main Menu 职业选择卡改用新立绘
  - CombatScene 玩家立绘改用新立绘
- 美术规范：
  - 新增 `doc/art-direction.md`，明确后续角色与卡牌插画以 2D anime / gacha RPG illustration 为基准
  - 排除照片写实、欧美写实概念图、3D 渲染感，防止后续资源方向漂移
  - 固化角色性别设定：灵能者、黑客、共生体均为女性角色
  - 固化清洁度与动作要求：角色本体低噪点、少微纹理，四职业主视觉必须使用差异化动态姿态
  - 共生体重新设计为银白短发女性寄生宿主，避免与黑客的黑发高马尾/数据战士轮廓撞型
  - 黑客重新精调为自信轻佻的女性入侵者，衣装改为低反光哑光黑材质与少量青色数据光
- 动态立绘试点：
  - 确认 Godot UI 层动态立绘采用 PNG 帧序列，而不是 GIF
  - 建立 25fps / 1 秒标准：每个战斗动作至少 25 张透明 PNG；待机为 1 秒循环，其他动作为一次性序列
  - 新增四职业战斗待机循环：`resources/textures/characters/animations/<class>/combat_idle/frame_00.png` 至 `frame_24.png`
  - 新增四职业攻击 / 加甲 / 职业核心特效序列：`attack`、`gain_armor`、先锋 `overload`、灵能者 `resonance`、黑客 `protocol_stack`、共生体 `erosion`
  - 战斗动态立绘使用透明背景，并改为朝右面向敌人的战斗姿态
  - 四职业待机动作区分：先锋重装呼吸与反应炉脉冲、灵能者悬浮共鸣、黑客轻佻数据操控、共生体寄生体脉动蓄势
  - `CombatScene` 支持优先加载职业 combat idle 帧序列，未提供动画的职业自动回退静态 PNG；玩家出攻击牌、加甲牌、职业核心/能力牌时会临时播放对应动作序列并回到待机
- 卡面表现：
  - 紧凑战斗卡面加入低透明度职业人物底图
  - 保留现有每张卡的 `artPath` 插画层作为前景，后续可逐张替换独立 PNG/WebP
- 动态立绘基础：
  - 战斗内玩家立绘保留呼吸缩放
  - 新增职业色光晕脉冲与扫描线，先形成“活着的立绘”基础效果
- 验证：
  - `dotnet test` = **54/54 pass**
  - Phase 3 UI smoke = **124 checks pass**
  - Phase 3 Flow smoke = **19 checks pass**
  - Phase 3 Interaction smoke = **24 checks pass**
  - Phase 3 Act smoke = **120 checks pass**

---

### 2026-05-10 Phase 4.1 站位系统深化：敌人位移闭环

- 接入“把敌人推入后排 → 近战敌失能一回合”的最小可玩闭环：
  - `vanguard_impact_shell`（冲击弹）新增 `reposition` 效果，将目标推入后排
  - 战斗核心已有的近战敌后排失能逻辑由新测试覆盖，避免后续重构漂移
- 强化 Combat UI 的敌方站位表现：
  - `EnemyArea` 动态生成 `EnemyBackLane` / `EnemyFrontLane`
  - 敌人面板会按 `FormationSystem` 的当前位置进入对应 lane；后续被推位时刷新会移动到新 lane
  - UI smoke 增加 lane 渲染检查
- 新增核心测试：
  - `tests/RogueCardGame.Tests/Phase4FormationTests.cs`
  - 覆盖敌人 `preferredRow` 初始化、近战不能越过前排、冲击弹推位、被推后排的前排近战敌失能且不造成伤害
- 验证：
  - `dotnet test` = **54/54 pass**（Godot SourceGenerator warning 仍为测试项目环境噪音）
  - Phase 3 UI smoke = **36 checks pass**
  - Phase 3 Flow smoke = **19 checks pass**
  - Phase 3 Interaction smoke = **24 checks pass**
  - Phase 3 Act smoke = **120 checks pass**

---

### 2026-05-10 Phase 3.8 完整 Act 自动验收

- 新增 Godot headless 完整 Act smoke runner：
  - `scripts/tools/Phase3ActSmokeRunner.cs`
  - `scenes/testing/Phase3ActSmoke.tscn`
- 覆盖 Act 1 连续路线：地图节点推进、自动战斗、奖励处理、事件选择、休息处理、Boss 战胜利，并验证 Boss 后进入 Act 2。
- runner 使用固定测试补给、休息点补给刷新、Boss 前/中维修药剂保护来稳定自动玩家；核心流程仍走真实场景、真实战斗管理器、真实奖励与地图推进。
- 验证：Godot headless Act smoke = **120 checks pass**（rooms=15, combats=9, rewards=9, events=3, rests=3）。

---

### 2026-05-10 Phase 3.7 分支交互验收

- 新增 Godot headless 分支交互 smoke runner：
  - `scripts/tools/Phase3InteractionSmokeRunner.cs`
  - `scenes/testing/Phase3InteractionSmoke.tscn`
- 覆盖 Phase 3 分支 UI 的点击行为与 run state 变化：
  - 奖励页：点击卡牌奖励后加入牌组，跳过按钮进入返回地图状态
  - 商店：点击卡牌商品后消耗金币并加入牌组
  - 事件：点击可用选项后显示结果文本与继续按钮
  - 休息：点击升级流程，确认后卡牌进入升级状态
  - 休息：点击治疗后玩家 HP 上升
- runner 与其他 smoke 一样会备份/恢复 `user://saves/`，并在每个分支场景卸载时清理音频播放资源，保持 headless 输出干净。
- 验证：Godot headless interaction smoke = **24 checks pass**。

---

### 2026-05-10 Phase 3.7 场景流转验收

- 新增 Godot headless 场景流转 smoke runner：
  - `scripts/tools/Phase3FlowSmokeRunner.cs`
  - `scenes/testing/Phase3FlowSmoke.tscn`
- 覆盖真实场景路由：
  - 启动测试 run 并进入 `MapScene`
  - 点击地图中的可达战斗节点，验证进入 `CombatScene`
  - 在战斗场景内自动选择可打出的卡牌、结算敌方回合，直到击败首个战斗节点
  - 验证战斗胜利进入 `RewardScene`
  - 点击奖励页 `SkipBtn`，验证回到 `MapScene`
  - 校验 `CurrentSceneId`、当前地图节点、`FloorsCleared`、已访问节点状态在场景切换后保持一致
- runner 会等待 `SceneManager` 淡入淡出结束后再推进下一步，避免 transition 中的二次切场景被忽略。
- 验证：Godot headless flow smoke = **19 checks pass**。

---

### 2026-05-10 Phase 3.7 UI 场景级验收

- 新增 Godot headless 场景级 smoke runner：
  - `scripts/tools/Phase3UiSmokeRunner.cs`
  - `scenes/testing/Phase3UiSmoke.tscn`
- 覆盖 Phase 3 关键 UI 场景加载与动态内容渲染：
  - 主菜单：基础按钮与职业选择网格
  - 地图：节点/路径容器与顶部信息
  - 战斗：敌人、开局手牌、结束回合、站位切换、牌堆信息
  - 奖励：金币奖励、卡牌奖励、跳过按钮
  - 商店：卡牌、药水/植入体、服务区
  - 事件：标题与延迟生成的选择项
  - 休息：HP、休息、升级、卡牌列表容器
- runner 会在执行前备份 `user://saves/`，结束后恢复，避免 UI 验收覆盖本机存档。
- 验证：
  - `dotnet build RogueCardGame.csproj` = **0 error / 0 warning**
  - Godot headless UI smoke = **36 checks pass**

---

### 2026-05-10 Phase 3.7 小步推进

- 修复战斗胜利奖励流转：
  - 普通/精英/Boss 金币由 `RunState.OnCombatVictory` 统一发放并记录 `LastCombatGoldReward`，奖励页只展示实际已发金额，避免双发或显示金额漂移。
  - Act 1/2 Boss 胜利后进入 `RewardScene`，领奖后再 `AdvanceAct()` 进入下一幕；最终 Boss 仍直接进入胜利结算。
  - `LastCombatGoldReward` 已纳入 RunState 存档，支持从奖励页继续游戏。
  - 选择卡牌奖励后不再自动离开奖励页，避免精英/Boss 的植入体或药水奖励来不及领取。
- `battlefieldInterface` 运行时 hook 已验证：换位免费，换位后下一张牌费用 -1。
- 接入 `precognitionModule` 战斗场景选择 UI：偶数回合窥视抽牌堆顶 2 张，玩家选择 1 张置于牌堆顶。
- 新增核心层 Act 烟雾测试：沿地图路径推进一整幕，验证战斗节点能创建敌人/CombatManager，Boss 后能进入下一幕。
- 验证：`dotnet test` = **52/52 绿**（仍有测试项目 Godot SourceGenerator warning，不影响结果）。

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
- [x] battlefieldInterface 运行时 hook（换位免费 + 下一张牌 -1 费）

### 事件系统

- [x] 11 个事件，分支选择 + 概率后果 + 条件判断
- [x] 效果类型：gainGold / loseGold / gainHp / loseHp / gainMaxHp / loseMaxHp / gainPotion / losePotion / upgradeCard / removeCard / gainImplant / gainCard / healPercent / loseHpPercent / loseGoldAll

### 战斗系统增强

- [x] Scry 机制：precognitionModule 每 2 回合触发，OnScryTriggered 事件 + CompleteScry 回调 + CombatScene 选择 UI
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
- [x] 奖励流转修复：金币只发一次；Boss 奖励结束后推进下一幕

### 3.5 事件系统 ✅
- [x] 11 个事件，覆盖 act 限制 / 一次性事件 / 条件分支

### 3.6 存档系统 ✅
- [x] SaveManager：SHA-256 校验，meta + run + settings 分离存储
- [x] SaveData / GameSettings 数据结构完整

### 3.7 待验证
- [x] Godot 场景级 UI smoke（主菜单 / 地图 / 战斗 / 奖励 / 商店 / 事件 / 休息，36 checks）
- [x] Godot 场景流转 smoke（地图 → 战斗自动出牌 → 奖励 → 地图，19 checks）
- [x] Godot 分支交互 smoke（奖励选牌 / 商店买卡 / 事件选择 / 休息升级 / 休息治疗，24 checks）
- [x] 真实完整 Act 验收（Act 1 连续路线 → Boss → Act 2，120 checks）
- [ ] Godot 编辑器首次运行调试（F5 实机可视化验证）
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
| 普通 ScryEffect 简化为抽牌 | CardEffect.cs | 预知模块已接 UI，卡牌通用 Scry 待后续统一 |
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
