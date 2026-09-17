---
name: mlsmoon-self-iterate
description: 收工时同步模块 skill，并强制单文件行数预算。适用于任务收尾、用户说自迭代 / 更新 skill / 文件太长 / 拆模块，或改完代码后 skill 可能过期、文件超过 300 行。
---

# 自迭代

本仓库的约定写在模块 skill 里。代码或流程变了，对应 skill 必须一起改。单文件默认 300 行；只有预算表里的少数例外可以更长，其余超限必须先拆再收工。

机器可读预算：同目录 `file-budget.json`。检查：`Scripts\file_budget.bat`（`-t -size` 也会跑）。

## 什么时候跑

做完任何会改产品行为、安装约定、界面壳或发版流程的任务，在声称完成之前跑完下面清单。用户点名「自迭代 / 更新 skill / 文件太长」时立刻跑。

不要把新约定堆进 `AGENTS.md` 或路由 skill。`AGENTS.md` 只留指针。

## 收工清单

1. 对照 diff 标出模块（catalog / install / workspace / access / ui / release / verify）。
2. 重读对应 `.agent/skills/mlsmoon-*/SKILL.md`。
3. 代码或文档和 skill 分叉了：改 skill，不要只改代码。
4. 新约定写进**那一个**模块 skill。路由表只加一行链接。
5. 出现新模块（新的职责面，不是多一个 helper）：新建 skill，登记到 `mlsmoon-skill-manager`。
6. `Scripts\file_budget.bat`。失败就按下面拆文件，不要先交工。
7. 动过预算表里的债务文件：禁止再变长。本次若还要往里加逻辑，先拆到 300 行以下，再把该路径移出 `debt`。
8. 要验收时读 `mlsmoon-verify`，开子 agent 跑 `-t`。

## 行数

物理行（含空行）。范围：`src/**/*.cs`、`src/**/*.xaml`、`Scripts/**/*.ps1`、`tests/**/*.cs`、`.agent/skills/**/*.md`。

| 档 | 行数 | 怎么做 |
|---|---|---|
| 默认上限 | 300 | 新文件和已合规文件不得越过 |
| 预警 | 250 | 还能改，但下一处职责切开就拆，不要再堆 |
| 必须拆 | >300 且不在例外 | 本轮拆完。不要只写「以后再拆」 |
| 例外 | `exceptions` | 只能是主题资源字典这类拆了会对不齐的文件。每条写 `reason`。不要把 ViewModel / 服务 / 测试当例外 |
| 债务 | `debt` | 历史超标。`ceiling` 是当前行数，只准少不准多。下次改职责必须拆掉并移出 |

`Themes/Controls.xaml` 是共享控件模板，允许超。`MainViewModel.cs`、`MainWindow.xaml`、安装/Git 服务**不是**例外。

## 怎么拆

按模块 skill 的边界拆，不要用 `partial class` 把同一堆逻辑切成薄片充数。

| 现在超了 | 拆向 |
|---|---|
| `MainViewModel.cs` | workspace / install / access / settings / update 各一个协作对象；主 VM 只留壳和命令转发 |
| `MainWindow.xaml` | 工作区栏、卡片列表、设置内容改成已有 `Controls/` 或独立 XAML |
| `SkillInstaller.cs` | GitHub 复制 / 局域网 / companion / 卸载 分开 |
| `SkillGit.cs` / `GitRemote.cs` | 状态判定留下，网络命令和 LAN 读 Readme 分开 |
| `SkillRowViewModel.cs` | 展示字段 vs Git/权限刷新 |
| `CatalogAndInstallTests.cs` | 按 catalog 合并 / 路径 / companion 拆类，仍禁止 mock |

SKILL.md 本身也走 300 行。细节放到同目录一层引用，不要套娃。

## 改 skill 时

- `description` 写第三人称，包含做什么、何时用
- 只写 agent 不会从代码里看出来的约定
- 删过时信息；例子用现在的字段名
- 本仓库维护 skill 不要写进 `catalog/skills.json`

## 不要做

- 超限文件再加一大段「顺便」
- 把债务路径改成例外来过检查
- 为了过预算删空行或把代码挤成一行
- 新建第二个总说明书
