# 开发进度记录

## 项目概况

- **项目名称**: RogueCardGame
- **开始时间**: 2026-04
- **设计版本**: v3
- **引擎**: Godot 4.6 + C# (.NET 8)
- **核心系统**: 站位 + 植入体（深度咬合）

## 当前阶段: Godot 集成重构

### 目标
将控制台原型全面重构为 Godot 4.6 驱动的 2D 卡牌游戏，所有核心系统改为 JSON 数据驱动。

### 已完成
- [x] 核心逻辑: 卡牌/战斗/敌人AI/牌组/地图/商店/事件/植入体/药水/环境/进度
- [x] 数据文件: 4职业卡牌/3幕敌人/植入体/药水/事件/平衡参数/三语翻译
- [x] 文档清理: 移除所有 v2 废弃设计内容
- [x] 切换 csproj 到 Godot.NET.Sdk/4.3.0
- [x] 职业系统 JSON 驱动化 (ClassDatabase + data/classes/*.json)
- [x] Autoload 系统 (GameManager/AudioManager/SceneManager)
- [x] 全部 Godot 场景: Main/MainMenu/Map/Combat/Shop/Event/Rest/Reward/GameOver/Victory/Settings
- [x] 战斗场景: 手牌展示/敌人面板/意图/伤害飘字/阵型切换/卡牌瞄准
- [x] 地图场景: 杀戮尖塔风格分叉路径/节点类型着色/路径连线
- [x] BalanceConfig: 数值外部化到 balance.json (阵型/仇恨/商店价格/地图生成)
- [x] FormationSystem/AggroSystem 常量从 balance.json 读取
- [x] RunState 使用 ClassDatabase 创建角色 (移除硬编码工厂方法)
- [x] 植入体系统: 3→2 槽位 (Neural + Core)
- [x] 移除废弃系统: 多人模块/PvP/旧UI/废弃房间类型
- [x] 赛博朋克 UI 主题: cyberpunk_theme.tres
- [x] 占位视觉资源: SVG 图标/卡牌模板/角色剪影/敌人/地图节点
- [x] 占位音频系统: PlaceholderAudioGenerator 自动生成静音 WAV
- [x] 完整游戏循环: 主菜单→职业选择→地图→战斗/事件/商店/休息→奖励→Boss→胜利/失败
- [x] 设置场景: 音量/全屏控制

### 进行中
- [ ] Godot 编辑器首次运行调试
- [ ] 真实音频/美术资源替换
- [ ] 卡牌数量扩充 (当前 ~65 张，目标 400+)
- [ ] 存档系统 Godot 适配
- [ ] 教程/引导系统

### 废弃/已删除
- 多人模块 (src/Multiplayer/) — 整个目录已删除
- PvP 系统 — 已删除
- 旧 UI 文件 (src/UI/) — 全部删除，由场景脚本替代
- Program.cs 控制台入口 — 已删除
- 协同链/入侵改造/适应性AI/遗物系统 — 已删除
- ModStation/DataTerminal 房间类型 — 已移除
- 躯体(Somatic)植入体槽位 — 已移除