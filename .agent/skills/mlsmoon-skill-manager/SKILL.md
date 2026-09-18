---
name: mlsmoon-skill-manager
description: Moon Game Dev Tool Manager 的模块路由。维护本仓库时先读本文件，再只打开本次改动对应的模块 skill。适用于改 catalog、安装路径、工作区、gh/NAS 权限、WPF 界面、发版，或不确定该读哪份 skill。
---

# Moon Game Dev Tool Manager

Windows WPF 应用。只安装 `catalog/skills.json`（及本机 override）里登记的条目：GitHub 上的 **Skill** / **Plugin** / **Package**，以及本机 override 里的局域网包（`source: lan`，通常是 Package）。不在清单里的本地文件一律不管。真实 NAS 地址禁止写进公开仓库。

GitHub 仓库：`MlsMoon/moon-game-dev-tool-manager`。界面产品名是 Moon Game Dev Tool Manager。程序集、AppData、安装目录仍用 `MlsmoonSkillManager`，避免已装用户丢设置。本仓库维护 skill 的目录名仍是 `mlsmoon-skill-manager`。

本文件只做路由。约定写在模块 skill 里，不要把细节再抄回这里。

## 先读哪份

| 改什么 | 读 |
|---|---|
| 公开/覆盖清单、`engines`、新增条目 JSON | `.agent/skills/mlsmoon-catalog/SKILL.md` |
| 安装路径、companion、标记、快照、复制 | `.agent/skills/mlsmoon-install/SKILL.md` |
| 工作区列表、卡片 Git 状态、冲突策略 | `.agent/skills/mlsmoon-workspace/SKILL.md` |
| `gh`、局域网探测、NAS SSH 三种失败 | `.agent/skills/mlsmoon-access/SKILL.md` |
| 主题、控件、设置对话框、单实例、动效 | `.agent/skills/mlsmoon-ui/SKILL.md` |
| `VERSION`、Inno、CI、GitHub Release、应用内更新 | `.agent/skills/mlsmoon-release/SKILL.md` |
| `-t`、子 agent、禁止 mock | `.agent/skills/mlsmoon-verify/SKILL.md` |
| 收工同步 skill、单文件行数、超限拆文件 | `.agent/skills/mlsmoon-self-iterate/SKILL.md` |

一次改多个模块就读多份。不要为了省事只读本路由。

本仓库的维护 skill **不要**写进 `catalog/skills.json`。

## 仓库地图

- `src/MlsmoonSkillManager.Core`：catalog、gh、工作区、复制安装
- `src/MlsmoonSkillManager.App`：WPF
- `catalog/skills.json`：公开清单（`skills` + `plugins` + `packages`）
- `catalog/skills.override.example.json`：覆盖格式；真实覆盖是已 gitignore 的 `catalog/skills.override.json`
- `.agent/skills/`：本仓库维护 skill（本文件是索引）
- `Scripts`：每个 `.ps1` 都有同名 `.bat`
- `CHANGELOG.md`：GitHub Release 正文的唯一来源
- `tests/MlsmoonSkillManager.Tests`：只留编译看不出来、且不 mock 的少量用例。不要再往里加

## 收工

改完先对照 `mlsmoon-self-iterate`：模块 skill 是否还准、单文件是否超 300 行。要验收再读 `mlsmoon-verify`，开子 agent 跑 `-t`。不要加传统单测或 mock。
