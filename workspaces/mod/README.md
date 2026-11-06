# 四元素 Mod 代码工作区

目标游戏 1.5.78.11833、API v77、运行目标 .NET Framework 4.7.2。使用项目内 .NET 10 SDK 编译，一个运行时 DLL，不依赖 Satchel 或 Unity Editor。

## 构建

从总项目根目录执行：

```powershell
./workspaces/mod/bootstrap.ps1
./workspaces/mod/build.ps1
python workspaces/mod/tools/prepare_assets.py
```

本机 Python 为 `E:/Envir/miniconda3/python.exe`，资源整理需要 Pillow。`bootstrap` 固定版本及校验值，不重置已有 API checkout；网络失败时尝试 localhost:7890。构建使用本地依赖，执行规则检查，输出 `artifacts/mod/HollowElements`；不修改游戏目录。

`tools/package-release.ps1` 根据通过的验收报告生成完整发布目录、校验清单与 zip。开发部署另用 `tools/deploy-development.ps1`，会先备份游戏覆盖文件及存档。

## 代码

- `src/Rules`：反应矩阵、敌人／玩家独立结算、附着、ICD、施放预算、持续事件、局部推进预算，无 Unity 引用。
- `src/Settings`：参数快照与校验、七槽配装、全局调试与表现设置。
- `src/Game/CombatRuntime.cs`：原版有效命中、伤害提交与持续事件；`AttackTracking.cs` 识别真实来源与同次施放。
- `EnemyClock.cs`：敌人自己的状态机、动画、物理和击退推进；不修改全局时间。
- `CombatPresentation.cs`、`PresentationAssets.cs`：血条、图标、池化跳字／特效、字形与 PCM 音频读取。
- `AttackTint.cs`：实例顶点颜色叠乘与原色恢复，不改共享材质。
- `WorkshopMenus.cs`：长椅与调试 uGUI；与原版的键鼠、手柄输入动作对接。
- `TrainingDummy.cs`：使用原版命中入口和短暂无敌规则的测试木桩。
- `DevelopmentBridge.cs`、`UnloadAudit.cs`：显式会话标记启用的本机验收工具；发布包不携带该标记。
- `tests`：直接编译同一套规则源码的控制台检查。

机制直接在 C# 修改，数值在 JSON 修改。`HollowElementsMod.RuleModifier` 接受 `IRuleModifier`，修改当次命中的攻击／反应参数副本；不污染基线或已经创建的感电。附着与超导分别使用组和施放预算。

## 实机检查

`tools/*_acceptance.py` 使用本机开发桥，验证实际游戏对象，不以纯公式检查替代游戏接入。覆盖真实白黑法术、剑技、多段编号、反应、玩家状态、长椅与参数面板、原版暂停、蓝血、对象池、Boss 局部减速、卸载与辐辉死亡返回。输出在 `artifacts/acceptance`。

开发标记只在主动部署时创建。常规测试阻止存档写入；保存读取检查将游戏存档与 API 配装文件都重定向到工作区。不要把 `development-session.json` 复制进发布包。
