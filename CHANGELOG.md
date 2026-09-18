# Changelog

GitHub Release 正文只从本文件抽取对应版本段落。每个已发布版本必须有一节：

```
## [X.Y.Z] - YYYY-MM-DD
```

写用户能感知的变化。不要只放 `Full Changelog: vA...vB` 那种对比链接。发版脚本见 `Scripts/release_notes.ps1`。

## [0.2.4] - 2026-09-18

Package 独立成类，卡片能分清本机和远端谁超前，打开目录和启动扫描按一次算一次。

### Catalog

- 清单增加 **Package**，和 Skill / Plugin 分开。局域网 URP 一类条目走 Package，不再标成 Plugin。
- companion skill 仍随插件安装，不单独出现在清单里。

### 工作区

- 默认 Skill 根是 `.agents`。旧 `.agent` 只当磁盘别名；也会检测 `.codex`。
- 工作区里带 `.git` 的副本算已安装，不会整目录覆盖。
- 文件树已经等于远端 tip 时显示已对齐，不再因为空 SHA 显示「落后」。
- 卡片对照 VS Code：**↓ Pull** / **↑ Push** 常驻，可点时亮、不可点时灰。不要再写「更新到远端」。

### 界面

- 打开软件后进度条只走一轮。
- 「打开目录」会打开资源管理器。
- 权限（可访问 / public）改用角标，不再裸绿色字。
- DEV 设置没有「更新」页，也不会在应用内检查或覆盖安装。
- 扫描时整张卡片是进度条，不再只在正文里写「正在对照」。

## [0.2.3] - 2026-09-17

公开 catalog 不再收录局域网地址。这是仓库历史重置后的第一个 GitHub Release。

### Catalog

- 公开 `catalog/skills.json` 只收 GitHub 上的 Skill / Plugin。
- 局域网条目写本机已 gitignore 的 `catalog/skills.override.json`，不进公开仓库。
- 公开仓库的覆盖格式写在 `catalog/skills.override.example.json`。
- 安装包和便携 `catalog` 都不带真实覆盖文件。

### 安装器

- Setup 向导始终出现「选择目标位置」。已经装过时也会显示，并预填上次路径。
- 默认目录仍是 `%LocalAppData%\MlsmoonSkillManager`。当前用户权限，选不到需要管理员的 `Program Files`。

### 发布说明

- GitHub Release 正文来自本文件对应版本段落。
- 不再把自动生成的对比链接当作唯一说明。

## [0.2.2] - 2026-09-17

安装器可以选目录。Release 说明改为仓库里的手写 changelog。局域网包改走本机 override。

### 安装器

- Setup 向导**始终**出现「选择目标位置」。本机已经用同一 AppId 装过时也会显示，不再跳过。
- 升级时预填上次安装路径；第一次安装的默认位置仍是 `%LocalAppData%\MlsmoonSkillManager`。
- 仍以当前用户权限安装，选不到需要管理员的 `Program Files`。

### 发布说明

- GitHub Release 正文来自本文件对应版本段落，由 `release.yml` 抽取。
- 不再把 GitHub 自动生成的 `Full Changelog: vX.Y.Z...vX.Y.Z` 当作唯一说明。

### Catalog

- 公开 `catalog/skills.json` 只收 GitHub 上的 Skill / Plugin。
- 局域网条目（`source: lan`）只写已 gitignore 的 `catalog/skills.override.json`。公开仓库格式可以写在 `skills.override.example.json`。安装包不带真实覆盖文件。

## [0.2.1] - 2026-09-17

局域网包、工作区 Git 同步、设置页与应用内更新。这是 0.2.0 之后的功能版本；当时 Release 只生成了对比链接，说明补记于此。

### 工作区与 Git

- 没有打开有效工作区时只能浏览清单，不能安装、更新或卸载。
- 打开工作区后，卡片显示当前分支、是否已是最新、工作区改过哪些已托管文件。
- 本地有改动且远端也有新提交（或工作区不干净却要换分支）视为冲突：只提示，不自动覆盖，需要在安装目录里手动处理。
- 卡片上可以选择分支。某个 Unity 大版本不能装的分支，按该局域网条目自己的约定处理。

### 局域网

- 对照 override 里的 `host` 探测是否在该网（本机同网段或主机端口可达），代码不写死默认地址。
- NAS「完全不可达」和「网络通但需要 SSH 密码或密钥」分开提示，不再都写成无权限。
- 有权限时读取仓库 Readme，安装到条目的 `installPath`，并按需合并 `Packages/manifest.json`。

### 界面

- 自定义标题栏，去掉系统白底顶栏。
- 「当前工作区」命令条放在工作区列表上方通栏。
- 设置改为左侧 Tab：外观、连接、更新、位置、关于、日志。GitHub / 局域网 / NAS 状态在「连接」，不在主窗口状态栏。
- 悬停只做透明度，不再缩放卡片或按钮。
- 条目带 Unity / Godot / 全引擎角标。PSD 与通用 3D 为全引擎；Spine GPU 插件仅 Unity。

### 应用更新与运行

- 设置「更新」对照 GitHub Release 的 Setup 包，可自动检查并下载安装。
- DEV 构建只提示新版本，不会覆盖本机已安装的 exe。
- 开发通道和安装通道各只开一个窗口；DEV 在版本号旁有角标。
- `Scripts` 下每个脚本都有对应 `.bat`。

### Catalog

- 局域网 Plugin 不进公开 catalog。
- Spine 随附 Skill 不再作为独立 Skill 出现，随插件安装，插件卸下时一并卸下。

## [0.2.0] - 2026-09-17

在 Skill 之外增加 Plugin 安装，产品名改为 Moon Game Dev Tool Manager。

- Unity Plugin 默认装到 `Assets/Plugins`，与 Agent Skill 目录分开。
- 公开仓库 SpineGpuSkinning 作为 Plugin 安装；仓库里的使用 / 开发 Skill 随插件写入，并且只在插件装进当前工作区后显示。
- GitHub 打安装包只接受已经在 `release` 分支上的 `vX.Y.Z` 标签。
- 仓库改名为 `MlsMoon/moon-game-dev-tool-manager`。程序集、AppData、安装目录仍用 `MlsmoonSkillManager`，避免已装用户丢设置。

## [0.1.0] - 2026-09-17

首个可用版本。

- 按 `catalog/skills.json` 把 MlsMoon 用 Git 管起来的 Agent Skill 装进指定工作区。
- 用本机 `gh` 检查仓库权限；看不到的私有条目只显示名字，并提示当前无权限访问。
- 提供 Inno Setup 安装包和便携 exe。开始菜单项为产品名，不需要管理员权限。
