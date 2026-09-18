---
name: mlsmoon-install
description: 维护 Skill / Plugin / Package 安装目标、`.mlsmoon` 身份、路由 Skill、标记文件与安装快照。适用于改 SkillInstaller、InstallSnapshot、UnityWorkspace、安装路径、随插件路由 Skill，或排查误删本地目录。
---

# 安装

代码：`SkillInstaller`、`SkillCopy` / `SkillCopyInstall`、`WorkspaceRepo`、`LanSkillInstall`、`InstallSnapshot`、`UnityWorkspace`、`AppPaths`。界面触发在 `MainViewModel` 的安装 / 更新 / 卸载。

Skill、Plugin、Package 必须走不同目录，不要混装。Plugin 和 Package 安装逻辑相同，只是默认目录和标记不同。不要扫描工作区里其它条目，也不要改无标记文件的目录。

## 目标

- Skill 默认根名：`.agents` → `{workspace}/.agents/skills/{installName}/`
- Skill 自动检测：`.claude`、`.grok`、`.codex`（旧版 Codex skill）
- 旧目录 `.agent` **不是**独立目标：勾选和设置里的 `.agent` 归一成 `.agents`。探测时若只有 `.agent/skills`，卡片仍算装在 `.agents`；新安装只写入 `.agents`，卸载会清掉带标记的 `.agent` 副本
- Plugin 默认：`{workspace}/Assets/Plugins/{installName}/`
- Package 默认：`{workspace}/Packages/{installName}/`
- Plugin / Package 可用 `installPath` 覆盖，必须是工作区内相对路径。GitHub 插件例：`Assets/Plugins/SpineGpuSkinning`；局域网包例：`Packages/UrpPackageIGP`
- 仓库根（`sourcePath` 为 `.`）的 Skill / Plugin / Package 安装目录都留下 `.git`。IGP Readme 说的「忽略 .git」是给 Plastic SCM 用的，不是本工具删 `.git`
- 随附 / 路由 Skill 按 `.mlsmoon`（优先）或旧 `companionSkills` 写到 Skill 根，不要当成父级仓库根

没有打开有效工作区时只能浏览清单，不能安装 / 更新 / 卸载。至少勾选一个 Skill 根（默认 `.agents`）。

## `.mlsmoon` 与路由 Skill

两套文件不要混：

- **`.mlsmoon/skill.json`**：skill / 插件自己的稳定身份，跟源码走。查找、打开目录、更新、卸载按 **`id`**，文件夹改名也能找到
- **`.mlsmoon-skill.json`**：本工具装上去时写的安装标记（commit / branch）。没有这份标记的目录禁止删除

路由 Skill：

- **不是**独立 catalog Skill，不能单独点「安装」
- 只在所属 Plugin / Package 已装进当前工作区后显示，角标为「路由」
- 卸载父级时：有本工具标记的子目录一并卸；没有标记的（项目自己维护的路由稿）跳过
- 权限跟所属 Plugin / Package
- 插件仓 `.mlsmoon/skill.json` 且 `kind: routing` 时，不要再把 `Skills~` 拷进 Skill 根。旧 catalog `companionSkills` 只给还没这份配置的条目当退路
- 工作区已有同一 `id` 则不覆盖 `SKILL.md`，只补 `.mlsmoon`

## 标记与快照

- Skill 标记：`.mlsmoon-skill.json`
- Plugin 标记：`.mlsmoon-plugin.json`（含 `commit` / `branch`）
- Package 标记：`.mlsmoon-package.json`。原先误标成 Plugin 的 Package 目录，卸载/扫描仍认 `.mlsmoon-plugin.json`
- 没有标记的目录禁止删除，避免误删用户自己放的文件
- `sourcePath` 为仓库根（Skill / Plugin / Package，不含 companion）：安装目录留下 `.git`。已有 `.git` 走 `WorkspaceRepo` 快进，禁止 `Directory.Delete` 整份重拷。没有 `.git` 就从缓存拷一份含 `.git` 的，或 `init` 并接上远端
- 卡片是否「已安装」只看目录在不在。是否「可更新」看工作区文件树是否已等于远端 tip，见 `mlsmoon-workspace`。不要写成「本地存在但非本工具安装」
- 安装快照（用于列出工作区改动）在 `%AppData%\MlsmoonSkillManager\snapshots\`。文件已与远端对齐时只写这份快照，不要为对齐去补写工作区标记
- GitHub 用 `gh` 克隆到缓存再同步到安装目录；局域网用 SSH 克隆到缓存再同步。登录过 NAS 则 git 带保存的密码

局域网 Package / Plugin 装完后按所选分支的 `manifest` 合并工作区 `Packages/manifest.json`。卸载时去掉对应 `file:` 前缀。

## 复制规则

- 仓库根同步留下 `.git`，仍跳过 `.github`、`bin`、`obj`、`.idea`。不要跳过 `Skills~` 或 `.mlsmoon`
- 装完 Skill 写 `.mlsmoon/skill.json` 身份（`id` / `kind` / `repo`）
- 非仓库根的拷贝（嵌套 companion 源）仍跳过 `.git`
- Skill 源目录必须有 `SKILL.md`（Plugin / Package 不要求）
- `sourcePath` 相对缓存根；`.` 表示仓库根
- `installPath` 必须落在工作区内，不能是绝对路径，不能含 `.` / `..` 段

缓存目录：`%LocalAppData%\MlsmoonSkillManager\cache\`。克隆缓存和 `catalog-meta.json`（卡片名称/简介）都在这里。设置里可清空；下次安装会重新拉仓库，下次扫描会按远端 commit 重读名称/简介。

## 不要做

- 把包内 `Skills~` 当成独立 catalog Skill
- 卸没有标记的目录
- 整目录覆盖带 `.git` 的安装目录
- 为了对齐 Plastic 而删掉 `.git`
- 扫描、改写清单以外的本地 skill / plugin / package

清单字段见 `mlsmoon-catalog`。卡片 Git 状态与冲突见 `mlsmoon-workspace`。改完跑 `-t -install`。
