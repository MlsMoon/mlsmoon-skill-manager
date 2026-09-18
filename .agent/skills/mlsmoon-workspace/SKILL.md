---
name: mlsmoon-workspace
description: 维护工作区列表、安装根选择、卡片 Git 状态与冲突策略。适用于改 WorkspaceBook、WorkspaceScanner、SkillGit 状态机、左侧工作区栏，或「本机落后远端 / 本机超前 / 已分叉 / 冲突 / 只能浏览」文案。
---

# 工作区

代码：`WorkspaceBook`、`WorkspaceScanner`、`WorkspaceInfo`、`SkillGit` / `WorkspaceRepo` / `InstallTreeAlign`、`SkillRoots`。界面：左侧工作区栏、卡片分支与状态。

## 列表

- `UserSettings.Workspaces` 记多个路径；`LastWorkspace` 只表示当前项
- 每个 `WorkspaceEntry` 自带 `SelectedRoots`，切换时恢复
- 旧设置只有 `lastWorkspace` 时由 `WorkspaceBook.Migrate` 迁入
- 左侧栏添加 / 切换 / 移除；移除只删本机列表，不动磁盘
- 路径比较用 `WorkspaceBook.PathsEqual`（规范化后再比，忽略大小写）
- 显示名用目录名；规范化失败时退回原字符串

## 打开之后

- 没有打开有效工作区时只能浏览清单，不能安装 / 更新 / 卸载
- 打开有效工作区后，对照已托管条目的安装快照和远端：列出工作区改过的文件、本机与远端谁超前、**Git HEAD 上的当前分支**。卡片下拉必须显示安装目录 `git branch --show-current`，不要用 Unity 推荐分支、安装标记或 catalog 默认值去盖掉。Unity 6 工程如果实际在 `master`，就显示 `master` 并保留「不能用 URP 14」警告，**不要**自动改成 `urp-17.5`。未安装、还没有 HEAD 时才用 `PickBranch`（按编辑器选；编辑器未知则用清单第一项，不要在未知时强行 `urp-17.5`）
- 启动只跑一轮权限+对照：构造函数只装工作区，窗口 Loaded 再 `ScanAllAsync`。不要构造时先对照、Loaded 再对照；也不要权限扫完 `ClearScan` 再 `InspectAllAsync` 从 0% 重来——卡片 overlay 会关一次再开，进度条看起来走两轮。权限和 Git 都按条目**串行**；同一张卡从排队→权限→对照是连续的 0–100，排队的卡保持低百分比「排队扫描…」。刷新 / NAS 登录走同一条 `ScanCatalogAsync`
- **先**对路由卡做本地 `project-skill` diff（进度「对照路由源…」），**然后**才对 Plugin / Package / 独立 Skill 做现有 Git inspect / Pull / Push。路由卡本身不走那套 Git；把文件同步进 `.mlsmoon/project-skill` 之后，改动出现在所属 Plugin 卡的本地 Git 里
- 对照用 GitHub `compare` / `merge-base`，不要只看 SHA 是否不同。卡片按钮对照 VS Code：**↓ Pull** / **Commit** / **↑ Push** 常驻，可点时强调色，不可点时灰。不要按状态藏按钮，也不要写「更新到远端」。仓库根安装目录还没有 HEAD 时多一个 **初始化 Git**；没接上之前 Pull / Commit / Push / 换分支都灰掉，打开目录和卸载仍可用。路由卡用「从源同步 / 同步到源」替换这三个 Git 按钮，见 `mlsmoon-install`
- ↓ Pull：安装目录有 `.git` 且已经有 HEAD、**并且就在所选分支上**时先出确认框，再 `fetch` + `merge --ff-only`，禁止整目录删除重拷。空 `git init` 不算接好，先点初始化。当前分支和所选不同时不要对另一条分支做 ff-only，也不要再叠一层 Pull 框：走现有切换警告
- 切换分支：用户在卡片改下拉后必须先出**强警告对话框**（取消则下拉回到当前 HEAD）。确认后 `git checkout` / 没有本地分支则 `checkout -b --track origin/<branch>`。工作区不干净就拒绝并提示冲突。扫描里的 `EnsureAttached` 可以 `fetch`，但 **`AlignHeadIfMatch` 只允许当前已经在该分支上时快进 HEAD**；没有 HEAD、或 HEAD 在别的分支时，扫描不得 `checkout` 到推荐分支
- Commit：设置「连接」里打开 **允许 Commit**，且工作区有本地改动（`LocalChanges` 或 `Conflict`）才可点。弹窗填写说明后只 `git add -A` + `commit`，**不要**顺手 Push
- ↑ Push：设置「连接」里打开 **允许 Push**，且状态是干净的 `Ahead` 才可点。先出确认框，再 `git push origin <branch>`。不要把未提交改动混进 Push
- 有本地修改且本机落后或已分叉（或想换分支但工作区不干净）算 **冲突**：只提示，不自动更新

`sourcePath` 为仓库根的 Skill / Plugin / Package（不含 companion）安装目录是真正的 git 仓库。没有 `.git` 就从缓存带上 `.git`，或 `init` 并接上 catalog 的 `origin`，再 `fetch`。扫描只做到这一步，**不要**在扫描里 `reset --hard`。有 `.git` 但还没有 HEAD（拷进来的文件全是未跟踪、远端 tip 已经 fetch 到）算 **需要初始化 Git**，不要显示「落后 N 个提交」。用户点 **初始化 Git** 必须先出确认框（树已经和远端一样也要弹，不要直接 `AdoptRemote`）。确认后：`update-ref` 到 `origin/<branch>` → `symbolic-ref HEAD` → `reset --hard` → `branch -u`。不要用 `checkout -f` 去覆盖未跟踪文件（git 仍会拒绝）。`reset --hard` 会按远端覆盖同名文件，未跟踪路径（如 `.mlsmoon/`）留下。初始化只动该安装目录，不要因为没勾选 Skill 安装根就拒绝 Plugin / Package。跑的时候对话框和卡片进度条一起走，不要只在状态栏打字。

## 卡片状态

安装目录在不在、远端 tip 是什么，以 Git 为准。有没有 `.mlsmoon-*.json` 不影响「算已安装」。**要不要 Pull 先看文件树是不是已经等于远端 tip**，不要只看标记里的 SHA。Plastic、手拷、脏工作区只要文件齐了、**并且已经有 HEAD**，才是 `Current`。

对照顺序：

1. 仓库根：对照前先 `EnsureAttached` + `fetch`（没有 `.git` 就补）。Plugin / Package 同样如此
2. 工作区文件树 vs 远端 tip。优先 `git diff --quiet`；否则用本机缓存哈希（忽略 `.git`、标记等）
3. 树相同且 **已有 HEAD**、**git status 也干净**（忽略本工具标记）才是 `Current`。HEAD 若还旧、且当前分支就是对照的那条，只把 HEAD 快进到远端，不改文件。不要因为文件树碰巧等于另一条分支的 tip 就把 InstalledBranch 改成那条。AppData 记快照，**不要**为对齐去写工作区标记。树相同但还没有 HEAD → `NeedsAttach`，等用户点初始化，扫描不要擅自 `reset --hard` / `checkout` / `git add`
4. 树不同再比本机 SHA（工作区 HEAD 优先，其次标记）
5. 缺本机 SHA、又比不了树 → `Unclear`，**不要**把空 SHA 当成 `Behind`
6. 干净且 SHA 落后 → `Behind`，↓ Pull 走 `fetch` + `merge --ff-only`
7. 脏工作区且远端有新提交 → `Conflict`，不自动快进

祖先关系对照 GitHub compare：`compare/{本机}...{远端}` 的 `ahead` = 本机落后，`behind` = 本机超前，`diverged` = 分叉。局域网或 API 失败时用缓存仓库 `rev-list --left-right --count` / `merge-base --is-ancestor`。判断不了就写 **无法判断谁新**，不要假装可以更新。

`SkillGitStatus.Decide(installed, hasLocal, relation, branchDiffers)` 不管 `NeedsAttach`。没有 HEAD 时 `SkillGit` 直接标 `NeedsAttach`，不要走进 `Decide`。

| 条件 | 状态 | 卡片 |
|---|---|---|
| 未安装 | `NotInstalled` | （无） |
| 已装、可 attach、有 `.git`、没有 HEAD | `NeedsAttach` | 需要初始化 Git：还没有接上远端分支 |
| 本地改动 +（本机落后 / 分叉 / 换分支） | `Conflict` | 有冲突 |
| 仅本地改动（含本机超前但工作区脏） | `LocalChanges` | 远端没有新提交 / 远端落后本机 |
| 干净但所选分支不同 | `BranchSwitch` | 当前在 A，将切换到 B；需确认 |
| 干净且本机落后 | `Behind` | ↓ Pull · 本机落后远端 N 个提交 |
| 干净且本机超前 | `Ahead` | ↑ Push · 本机超前远端 |
| 干净且分叉 | `Diverged` | ↓ N  ↑ M · 已分叉 |
| 提交不同但无法判断 | `Unclear` | 无法判断谁新 |
| 其余已装且干净 | `Current` | 已与远端对齐 |

`CanUpdate` / **↓ Pull** 仅 `Behind` 或 `BranchSwitch`。点 Pull 时若所选分支不是当前 HEAD，先走切换警告，不要 `merge --ff-only`。同分支 Pull 也要先确认。**Commit** 仅设置打开了允许 Commit，且有本地改动。**↑ Push** 仅设置打开了允许 Push，且状态为干净的 `Ahead`。`NeedsAttach` 只能点 **初始化 Git**。分叉、无法判断都不要快进覆盖。冲突只弹说明；冲突时仍可 Commit（不自动 Push / Pull）。

本地改动：安装目录已有 HEAD 时以 `git status --porcelain` 为准（忽略 `.mlsmoon-*.json`）。没有 AppData 快照不能当成干净。扫描禁止 `git add` / `git reset`。快照只给没有可用 Git 工作树的副本。快照目录 `%AppData%\MlsmoonSkillManager\snapshots\`。

## 探测根

`WorkspaceScanner` 列出 `.agents`、`.claude`、`.grok`、`.codex`。默认根是 `.agents`（可以还不存在）。旧 `.agent` 不单独列出，只当作 `.agents` 的磁盘别名。勾选变化写回当前 `WorkspaceEntry.SelectedRoots`，读到 `.agent` 会归一成 `.agents`。

## 不要做

- 移除工作区时删磁盘上的项目
- 冲突时自动 checkout / 覆盖
- 整目录删掉带 `.git` 的安装目录再重拷
- 把空 SHA 直接当成可 Pull
- 扫描时空 `git init` 自动 `reset --hard`
- 扫描里 `checkout` 到另一条分支
- 扫描里 `git add` / `git reset` 去对照文件树
- 用 `merge --ff-only` 冒充切分支
- 为迁入/去重再堆 xUnit（已有 `WorkspaceBookTests` 只当工具跑）

安装标记见 `mlsmoon-install`。远端权限见 `mlsmoon-access`。工作区列表或 Git 对照策略变了再按 `mlsmoon-verify` 跑 `-t -workspace`；动到文件树/冲突判定再加 `-sync`。
