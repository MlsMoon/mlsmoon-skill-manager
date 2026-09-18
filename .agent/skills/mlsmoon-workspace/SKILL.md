---
name: mlsmoon-workspace
description: 维护工作区列表、安装根选择、卡片 Git 状态与冲突策略。适用于改 WorkspaceBook、WorkspaceScanner、SkillGit 状态机、左侧工作区栏，或「本机落后远端 / 本机超前 / 已分叉 / 冲突 / 只能浏览」文案。
---

# 工作区

代码：`WorkspaceBook`、`WorkspaceScanner`、`WorkspaceInfo`、`SkillGit` / `InstallTreeAlign` / `SkillCopy`、`SkillRoots`。界面：左侧工作区栏、卡片分支与状态。

## 列表

- `UserSettings.Workspaces` 记多个路径；`LastWorkspace` 只表示当前项
- 每个 `WorkspaceEntry` 自带 `SelectedRoots`，切换时恢复
- 旧设置只有 `lastWorkspace` 时由 `WorkspaceBook.Migrate` 迁入
- 左侧栏添加 / 切换 / 移除；移除只删本机列表，不动磁盘
- 路径比较用 `WorkspaceBook.PathsEqual`（规范化后再比，忽略大小写）
- 显示名用目录名；规范化失败时退回原字符串

## 打开之后

- 没有打开有效工作区时只能浏览清单，不能安装 / 更新 / 卸载
- 打开有效工作区后，对照已托管条目的安装快照和远端：列出工作区改过的文件、本机与远端谁超前、当前分支。卡片上可选分支。启动只跑一轮权限+对照：构造函数只装工作区，窗口 Loaded 再扫；不要先对照完再扫第二遍
- 对照用 GitHub `compare` / `merge-base`，不要只看 SHA 是否不同。卡片按钮对照 VS Code：**↓ Pull** / **↑ Push** 常驻，可点时强调色，不可点时灰。不要按状态藏按钮，也不要写「更新到远端」。管理器装进去的副本没有 `.git`，Push 只说明超前，不会真的推远端
- 有本地修改且本机落后或已分叉（或想换分支但工作区不干净）算 **冲突**：只提示，不自动更新，用户在安装目录里手动处理

管理器自己装的副本不含 `.git`。Git 命令封装在 `GitRemote` / `GhCli` / `SkillGit`：`ls-remote`、分支列表、远端 tip、按分支 fetch/checkout。工作区里用户自己放的带 `.git` 目录要认出来，但不要在里面 `fetch` / `checkout`，更不要整目录覆盖。

## 卡片状态

安装目录在不在、远端 tip 是什么，以 Git 为准。有没有 `.mlsmoon-*.json` 不影响「算已安装」。**要不要 Pull 先看文件树是不是已经等于远端 tip**，不要只看标记里的 SHA。Plastic、手拷、脏工作区只要文件齐了，就是 `Current`。

对照顺序：

1. 工作区文件树 vs 远端 tip。本机缓存已停在该 tip 时用同一套快照哈希（忽略 `.git`、标记等）；否则若目录里有 `.git` 且已有该 commit 对象，用 `git diff --quiet <远端>`（只读，不 fetch）
2. 树相同 → `Current`。AppData 记下「已按远端 tip 对齐」的快照，**不要**往工作区写标记
3. 树不同再比本机 SHA：标记优先；没有标记才读目录里的 HEAD
4. 缺本机 SHA、又比不了树 → `Unclear`，**不要**把空 SHA 当成 `Behind`
5. 缺本机 SHA、树已比过且不同、目录没有 `.git` → 按内容落后，可以 Pull
6. 目录带 `.git` 时，即使 SHA 落后也不要快进覆盖：卡片 `Unclear`，请用户在该目录里自行更新

祖先关系对照 GitHub compare：`compare/{本机}...{远端}` 的 `ahead` = 本机落后，`behind` = 本机超前，`diverged` = 分叉。局域网或 API 失败时用缓存仓库 `rev-list --left-right --count` / `merge-base --is-ancestor`。判断不了就写 **无法判断谁新**，不要假装可以更新。

`SkillGitStatus.Decide(installed, hasLocal, relation, branchDiffers)`：

| 条件 | 状态 | 卡片 |
|---|---|---|
| 未安装 | `NotInstalled` | （无） |
| 本地改动 +（本机落后 / 分叉 / 换分支） | `Conflict` | 有冲突 |
| 仅本地改动（含本机超前但工作区脏） | `LocalChanges` | 远端没有新提交 / 远端落后本机 |
| 干净但所选分支不同 | `BranchSwitch` | 将切换到… |
| 干净且本机落后 | `Behind` | ↓ Pull · 本机落后远端 N 个提交 |
| 干净且本机超前 | `Ahead` | ↑ Push · 本机超前远端 |
| 干净且分叉 | `Diverged` | ↓ N  ↑ M · 已分叉 |
| 提交不同但无法判断 | `Unclear` | 无法判断谁新 |
| 其余已装且干净 | `Current` | 已与远端对齐 |

`CanUpdate` / **↓ Pull** 仅 `Behind` 或 `BranchSwitch`。**↑ Push** 只在 `Ahead` 可点，用来说明不能推远端。分叉、无法判断都不要快进覆盖。冲突只弹说明。

本地改动来自快照对照，不是 `git status`。快照在 `%AppData%\MlsmoonSkillManager\snapshots\`。

## 探测根

`WorkspaceScanner` 列出 `.agents`、`.claude`、`.grok`、`.codex`。默认根是 `.agents`（可以还不存在）。旧 `.agent` 不单独列出，只当作 `.agents` 的磁盘别名。勾选变化写回当前 `WorkspaceEntry.SelectedRoots`，读到 `.agent` 会归一成 `.agents`。

## 不要做

- 移除工作区时删磁盘上的项目
- 冲突时自动 checkout / 覆盖
- 在工作区安装副本里初始化 `.git`
- 覆盖带 `.git` 的用户副本，或把空 SHA 直接当成可 Pull
- 为迁入/去重再堆 xUnit（已有 `WorkspaceBookTests` 只当工具跑）

安装标记见 `mlsmoon-install`。远端权限见 `mlsmoon-access`。改完跑 `-t -workspace -sync`。
