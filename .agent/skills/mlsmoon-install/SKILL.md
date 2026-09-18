---
name: mlsmoon-install
description: 维护 Skill / Plugin / Package 安装目标、companionSkills、标记文件与安装快照。适用于改 SkillInstaller、InstallSnapshot、UnityWorkspace、安装路径、随插件 Skill，或排查误删本地目录。
---

# 安装

代码：`SkillInstaller`、`SkillCopy`、`LanSkillInstall`、`InstallSnapshot`、`UnityWorkspace`、`AppPaths`。界面触发在 `MainViewModel` 的安装 / 更新 / 卸载。

Skill、Plugin、Package 必须走不同目录，不要混装。Plugin 和 Package 安装逻辑相同，只是默认目录和标记不同。不要扫描工作区里其它条目，也不要改无标记文件的目录。

## 目标

- Skill 默认根名：`.agents` → `{workspace}/.agents/skills/{installName}/`
- Skill 自动检测：`.claude`、`.grok`、`.codex`（旧版 Codex skill）
- 旧目录 `.agent` **不是**独立目标：勾选和设置里的 `.agent` 归一成 `.agents`。探测时若只有 `.agent/skills`，卡片仍算装在 `.agents`；新安装只写入 `.agents`，卸载会清掉带标记的 `.agent` 副本
- Plugin 默认：`{workspace}/Assets/Plugins/{installName}/`
- Package 默认：`{workspace}/Packages/{installName}/`
- Plugin / Package 可用 `installPath` 覆盖，必须是工作区内相对路径。GitHub 插件例：`Assets/Plugins/SpineGpuSkinning`；局域网包例：`Packages/UrpPackageIGP`
- Plugin / Package 安装时跳过 `Skills~`；仓库里的 Agent skill 用 `companionSkills` 随父级写入 Skill 目标

没有打开有效工作区时只能浏览清单，不能安装 / 更新 / 卸载。至少勾选一个 Skill 根（默认 `.agents`）。

## companionSkills

- **不是**独立 Skill：目录里不要单独出现，也不能单独点「安装」
- 随附 Skill 只在所属 Plugin / Package 已装进当前工作区后显示，标记为「随插件」
- 卸载父级时一并卸随附 Skill；随附条目仍可单独更新 / 卸载
- 随附 Skill 的权限跟所属 Plugin / Package

## 标记与快照

- Skill 标记：`.mlsmoon-skill.json`
- Plugin 标记：`.mlsmoon-plugin.json`（含 `commit` / `branch`）
- Package 标记：`.mlsmoon-package.json`。原先误标成 Plugin 的 Package 目录，卸载/扫描仍认 `.mlsmoon-plugin.json`
- 没有标记的目录禁止删除，避免误删用户自己放的文件
- 目标目录已有 `.git` 时禁止整目录覆盖（安装 / Pull 都拒绝）。这是用户自己的工作副本，文件齐了就不必动；要更新请在该目录里自行 pull
- 卡片是否「已安装」只看目录在不在。是否「可更新」看工作区文件树是否已等于远端 tip，见 `mlsmoon-workspace`。不要写成「本地存在但非本工具安装」
- 安装快照（用于列出工作区改动）在 `%AppData%\MlsmoonSkillManager\snapshots\`，不是工作区里的 `.git`。文件已与远端对齐时只写这份快照，不要补写工作区标记
- 管理器自己装的副本不含 `.git`：GitHub 走 `gh` 克隆到缓存再复制；局域网走 `git archive --remote` 到缓存再复制

局域网 Package / Plugin 装完后按所选分支的 `manifest` 合并工作区 `Packages/manifest.json`。卸载时去掉对应 `file:` 前缀。

## 复制规则

- 跳过：`.git`、`.github`、`.vs`、`bin`、`obj`、`.idea`
- Plugin / Package 额外跳过 `Skills~`
- Skill 源目录必须有 `SKILL.md`（Plugin / Package 不要求）
- `sourcePath` 相对缓存根；`.` 表示仓库根
- `installPath` 必须落在工作区内，不能是绝对路径，不能含 `.` / `..` 段

缓存目录：`%LocalAppData%\MlsmoonSkillManager\cache\`。设置里可清空；下次安装会重新拉。

## 不要做

- 把 companion 当成独立 catalog Skill
- 卸没有标记的目录
- 覆盖带 `.git` 的目录，或在工作区副本里留 `.git` / 建远端跟踪
- 扫描、改写清单以外的本地 skill / plugin

清单字段见 `mlsmoon-catalog`。卡片 Git 状态与冲突见 `mlsmoon-workspace`。改完跑 `-t -install`。
