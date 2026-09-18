---
name: mlsmoon-self-iterate
description: 收工时同步模块 skill，并按可读性看待单文件体量。适用于任务收尾、用户说自迭代 / 更新 skill / 文件太长 / 拆模块，或改完代码后 skill 可能过期。
---

# 自迭代

本仓库的约定写在模块 skill 里。代码或流程变了，对应 skill 必须一起改。

机器可读体量提示：同目录 `file-budget.json`。检查：`Scripts\file_budget.bat`（`-t -size` 也会跑）。**只出提示，不因此失败。**

## 什么时候跑

做完任何会改产品行为、安装约定、界面壳或发版流程的任务，在声称完成之前跑完下面清单。用户点名「自迭代 / 更新 skill / 文件太长」时立刻跑。

不要把新约定堆进 `AGENTS.md` 或路由 skill。`AGENTS.md` 只留指针。

## 收工清单

1. 对照 diff 标出模块（catalog / install / workspace / access / ui / release / verify）。
2. 重读对应 `.agent/skills/mlsmoon-*/SKILL.md`。
3. 代码或文档和 skill 分叉了：改 skill，不要只改代码。
4. 新约定写进**那一个**模块 skill。路由表只加一行链接。
5. 出现新模块（新的职责面，不是多一个 helper）：新建 skill，登记到 `mlsmoon-skill-manager`。
6. `Scripts\file_budget.bat`。超 300 行只是提醒：职责已经混在一起、读不下去时再拆。
7. 改了 `src/` 或 `Scripts/`：读 `mlsmoon-verify`。先在对话里写出建议的 `-t` 级别并询问；用户没点名就按建议跑（通常至少 `-t -build`）。不要干等。默认不必开子 agent；只有用户点名或安装 / catalog / 工作区 Git / `ui_flow` 覆盖路径真的变了才开，且只带对得上的 `-t` 旗标

## 行数

物理行（含空行）。范围：`src/**/*.cs`、`src/**/*.xaml`、`Scripts/**/*.ps1`、`tests/**/*.cs`、`.agent/skills/**/*.md`。

300 行是**观感提示**，不是硬卡。`-t -size` 只打印 hint，exit 0。

| 档 | 怎么做 |
|---|---|
| 大约 250+ | 还能改。下一处如果是新职责，优先开新文件 |
| 大约 300+ | 看这个文件是不是已经混了好几件事。混了就按模块边界拆；单一职责、读得下去就留着 |
| `exceptions` | 主题资源字典这类拆了对不齐的文件。写 `reason` |
| `debt` | 已经偏长的历史文件，提醒下次顺手拆，**不禁止变长** |

拆的理由是可读、职责清楚，不是凑行数。

### 不要为了行数做的事

- 把一段说明拆成好多短句或另开一层文件，只为少几行
- 把几个判断挤成一行三元 / 嵌套 `?:` / 超长表达式
- 删空行、把代码粘成一行
- 用 `partial class` 把同一堆逻辑切成薄片充数

## 怎么拆

按模块 skill 的边界拆，只在文件已经难读时动手。

| 已经偏长 | 真要拆时往哪 |
|---|---|
| `MainViewModel.cs` | workspace / install / access / settings / update 各一个协作对象；主 VM 只留壳和命令转发 |
| `MainWindow.xaml` | 工作区栏、卡片列表、设置内容改成已有 `Controls/` 或独立 XAML |
| `SkillInstaller.cs` | GitHub 复制 / 局域网 / companion / 卸载 分开 |
| `SkillGit.cs` / `GitRemote.cs` | 状态判定留下，网络命令和 LAN 读 Readme 分开 |
| `SkillRowViewModel.cs` | 展示字段 vs Git/权限刷新 |
| `CatalogAndInstallTests.cs` | 按 catalog 合并 / 路径 / companion 拆类，仍禁止 mock |

SKILL.md 写清楚即可，不要为了行数再套一层文件。

## 批处理

`Scripts/` 里每个 `.ps1`（以及 `ui_flow.py`）都要有同名 `.bat`，完整覆盖那条命令：

- `cd /d "%~dp0.."` 回到仓库根
- 调用对应脚本并转发 **全部参数** `%*`
- `exit /b %ERRORLEVEL%`

不要只写半截、丢掉参数或返回码。双击或从资源管理器跑 bat，应和直接跑 ps1/py 一样。

## 改 skill 时

- `description` 写第三人称，包含做什么、何时用
- 只写 agent 不会从代码里看出来的约定
- 删过时信息；例子用现在的字段名
- 本仓库维护 skill 不要写进 `catalog/skills.json`

## 不要做

- 为了过 `-t -size` 去挤代码或拆文案
- 新建第二个总说明书
