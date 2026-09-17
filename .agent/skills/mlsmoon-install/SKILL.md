---
name: mlsmoon-install
description: 维护 Skill / Plugin 安装目标、companionSkills、标记文件与安装快照。适用于改 SkillInstaller、InstallSnapshot、UnityWorkspace、安装路径、随插件 Skill，或排查误删本地目录。
---

# 安装

代码：`SkillInstaller`、`InstallSnapshot`、`UnityWorkspace`、`AppPaths`。界面触发在 `MainViewModel` 的安装 / 更新 / 卸载。

Skill 与 Plugin 必须走不同目录，不要混装。不要扫描工作区里其它 skill / plugin，也不要改无标记文件的目录。

## 目标

- Skill 默认根名：`.agents` → `{workspace}/.agents/skills/{installName}/`
- Skill 自动检测：`.claude`、`.grok`、`.codex`（旧版 Codex skill）
- 旧目录 `.agent` **不是**独立目标：勾选和设置里的 `.agent` 归一成 `.agents`。探测时若只有 `.agent/skills`，卡片仍算装在 `.agents`；新安装只写入 `.agents`，卸载会清掉带标记的 `.agent` 副本
- Plugin 默认：`{workspace}/Assets/Plugins/{installName}/`
- Plugin 可用 `installPath` 覆盖，必须是工作区内相对路径。GitHub 插件例：`Assets/Plugins/SpineGpuSkinning`；局域网包例：`Packages/YourLanPlugin`
- Plugin 安装时跳过 `Skills~`；仓库里的 Agent skill 用 `companionSkills` 随插件写入 Skill 目标

没有打开有效工作区时只能浏览清单，不能安装 / 更新 / 卸载。至少勾选一个 Skill 根（默认 `.agents`）。

## companionSkills

- **不是**独立 Skill：目录里不要单独出现，也不能单独点「安装」
- 随附 Skill 只在所属 Plugin 已装进当前工作区后显示，标记为「随插件」
- 卸载 Plugin 时一并卸随附 Skill；随附条目仍可单独更新 / 卸载
- 随附 Skill 的权限跟所属 Plugin

## 标记与快照

- Skill 标记：`.mlsmoon-skill.json`
- Plugin 标记：`.mlsmoon-plugin.json`（含 `commit` / `branch`）
- 没有标记的目录禁止删除，避免误删用户自己放的文件
- 卡片是否「已安装 / 可更新」只看目录在不在和远端 Git，不要写成「本地存在但非本工具安装」
- 安装快照（用于列出工作区改动）在 `%AppData%\MlsmoonSkillManager\snapshots\`，不是工作区里的 `.git`
- 工作区副本不含 `.git`：GitHub 走 `gh` 克隆到缓存再复制；局域网走 `git archive --remote` 到缓存再复制

局域网 Plugin 装完后按所选分支的 `manifest` 合并工作区 `Packages/manifest.json`。卸载时去掉对应 `file:` 前缀。

## 复制规则

- 跳过：`.git`、`.github`、`.vs`、`bin`、`obj`、`.idea`
- Plugin 额外跳过 `Skills~`
- Skill 源目录必须有 `SKILL.md`（Plugin 不要求）
- `sourcePath` 相对缓存根；`.` 表示仓库根
- `installPath` 必须落在工作区内，不能是绝对路径，不能含 `.` / `..` 段

缓存目录：`%LocalAppData%\MlsmoonSkillManager\cache\`。设置里可清空；下次安装会重新拉。

## 不要做

- 把 companion 当成独立 catalog Skill
- 卸没有标记的目录
- 在工作区副本里留 `.git` 或在工作区建远端跟踪
- 扫描、改写清单以外的本地 skill / plugin

清单字段见 `mlsmoon-catalog`。卡片 Git 状态与冲突见 `mlsmoon-workspace`。改完跑 `-t -install`。
