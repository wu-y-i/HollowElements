# HollowElement

为《空洞骑士》引入元素反应的战斗模组。火、水、雷、冰在攻击与敌人之间交织，让熟悉的神居战斗拥有新的选择。



## 特色

- 为玩家与敌人加入元素附着，以及蒸发、融化、超载、感电、冻结等反应。
- 探索 Boss 专属机制、战斗提示与可调整的难度。
- 搭配独立护符、元素配置和游戏内图鉴，寻找适合自己的战斗方式。
- 可调整反应特效、伤害数字与其他表现设置。

## 使用

目标环境为 Windows 版《空洞骑士》1.5.78.11833 和 Modding API 77。安装匹配的 Modding API 后，将发布包中的 `HollowElements` 文件夹放入游戏的 `Mods` 目录；游戏内可通过背包的「元素系统」页或 F5 查看图鉴和配置。

## 构建

代码位于 `workspaces/mod`，运行所需的源资产位于 `workspaces/art/source`。在 PowerShell 中运行：

```powershell
./workspaces/mod/bootstrap.ps1
./workspaces/mod/build.ps1
python workspaces/mod/tools/prepare_assets.py
```

构建需要本机游戏文件、对应 Modding API 依赖和 Python Pillow；具体配置参见 [代码工作区说明](workspaces/mod/README.md)。构建输出与本地依赖不纳入仓库。

本项目是粉丝创作的非官方模组，与《空洞骑士》及《原神》的权利方无隶属关系。

