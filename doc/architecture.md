# 项目架构文档

> **设计版本**: v3 | **引擎**: Godot 4.6 + C# (.NET 8) | **设计文档**: core-gameplay-design.md

## 技术栈

| 组件 | 选型 | 说明 |
|------|------|------|
| 引擎 | Godot 4.6 (.NET) | 开源轻量 C# 支持 |
| 语言 | C# 12 (.NET 8) | 强类型 |
| SDK | Godot.NET.Sdk 4.3.0 | Godot C# 集成 |
| 序列化 | System.Text.Json | 数据加载存档 |
| 数据格式 | JSON | 卡牌敌人植入体职业平衡 |
| 本地化 | CSV | 中英日 |

## 核心架构模式

### 1. 数据驱动
所有游戏内容由 JSON 定义。零硬编码内容 ID。新增职业卡牌只需添加 JSON。

### 2. 事件总线
系统间通过 EventBus 解耦。核心事件: CardPlayed DamageDealt TurnStarted StatusApplied。

### 3. 逻辑表现分离
- src/Core/ 零 Godot 依赖可独立测试
- src/UI/ + scenes/ 负责 Godot 表现层
- scripts/autoload/ 桥接两层

### 4. Autoload 单例
- GameManager: RunState 游戏生命周期
- AudioManager: BGM/SFX 淡入淡出
- SceneManager: 场景切换过渡动画

### 5. 资源可替换
所有美术音频通过路径约定加载。替换只需同名放入对应目录。