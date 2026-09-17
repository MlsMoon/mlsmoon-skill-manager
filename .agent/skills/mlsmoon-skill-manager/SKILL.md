---
name: mlsmoon-skill-manager
description: 维护 Moon Game Dev Tool Manager 桌面应用、catalog 映射、Skill / Plugin 安装路径、gh 权限检查、Inno Setup 安装包与 develop/release CI。适用于新增 MlsMoon 托管 skill 或 Unity plugin、改安装目标、打包 Windows exe / Setup.exe，或排查私有仓库显示“当前无权限访问”。要跑验证、写 -t、-t -catalog / -install / -ui 时改读 mlsmoon-verify，不要加传统单测或 mock。
---

# Moon Game Dev Tool Manager

Windows WPF 应用。只安装 `catalog/skills.json`（及本机 override）里登记的条目：GitHub 上的 **Skill** / **Plugin**，以及本机 override 里的局域网包（`source: lan`）。不在清单里的本地文件一律不管。真实 NAS 地址禁止写进公开仓库。

GitHub 仓库：`MlsMoon/moon-game-dev-tool-manager`。界面产品名是 Moon Game Dev Tool Manager。程序集、AppData、安装目录仍用 `MlsmoonSkillManager`，避免已装用户丢设置。维护本仓库的 agent skill 目录名仍是 `mlsmoon-skill-manager`。

## 仓库地图

- `src/MlsmoonSkillManager.Core`：catalog、gh、工作区探测、复制安装
- `src/MlsmoonSkillManager.App`：WPF 界面
- `catalog/skills.json`：名字 ↔ 仓库的公开清单（`skills` + `plugins`）
- `.agent/skills/mlsmoon-skill-manager`：产品与安装约定（本文件）
- `.agent/skills/mlsmoon-verify`：验收。用户或 agent 写 `-t -xxx`，开子 agent 跑真命令，不 mock、不加 Fact
- `Scripts/rundev.bat`：本地 Debug（`--dev`）。`Scripts/verify.bat`：同样认 `-t -xxx`
- `Scripts` 里每个 `.ps1` 都有同名 `.bat`
- `CHANGELOG.md`：GitHub Release 正文的唯一来源
- `Scripts/installer.iss`：Inno Setup
- `Scripts/release_notes.ps1`：从 changelog 抽出当前 VERSION
- `.github/workflows/ci.yml`：`develop` / `release` 编译 + 便携包
- `.github/workflows/release.yml`：仅 `release` 分支上的 `vX.Y.Z` tag 打安装包并发 GitHub Release
- `tests/MlsmoonSkillManager.Tests`：只留编译看不出来、且不 mock 的少量用例。不要再往里加

## 工作区列表

- `UserSettings.Workspaces` 记多个路径；`LastWorkspace` 只表示当前项
- 每个 `WorkspaceEntry` 自带 `SelectedRoots`，切换时恢复
- 旧设置只有 `lastWorkspace` 时由 `WorkspaceBook.Migrate` 迁入
- 左侧栏添加 / 切换 / 移除；移除只删本机列表，不动磁盘
- 没有打开有效工作区时只能浏览清单，不能安装 / 更新 / 卸载
- 打开有效工作区后，对照已托管条目的安装快照和远端：列出工作区改过的文件、是否有更新、当前分支。卡片上可选分支
- 没有远端更新时卡片写 **已是最新**。有本地修改且远端也有新提交（或想换分支但工作区不干净）算 **冲突**：只提示，不自动更新，用户在安装目录里手动处理
- Git 命令封装在 `GitRemote` / `GhCli` / `SkillGit`：`ls-remote`、分支列表、远端 tip、按分支 fetch/checkout。工作区副本不含 `.git`

## 安装目标

Skill 与 Plugin 必须走不同目录，不要混装。

- Skill 默认根名：`.agent` → `{workspace}/.agent/skills/{installName}/`
- Skill 自动检测：`.claude`、`.grok`；若存在也列出 `.agents`
- Plugin 默认：`{workspace}/Assets/Plugins/{installName}/`
- Plugin 可用 `installPath` 覆盖，必须是工作区内相对路径。GitHub 插件例：`Assets/Plugins/SpineGpuSkinning`；局域网包例：`Packages/YourLanPlugin`
- Plugin 安装时跳过 `Skills~`；仓库里的 Agent skill 用 `companionSkills` 随插件写入 Skill 目标
- `companionSkills` **不是**独立 Skill：目录里不要单独出现，也不能单独点「安装」
- 随附 Skill 只在所属 Plugin 已装进当前工作区后显示，标记为「随插件」
- 卸载 Plugin 时一并卸随附 Skill；随附条目仍可单独更新 / 卸载
- 不要扫描工作区里其它 skill / plugin，也不要改无标记文件的目录
- Skill 标记：`.mlsmoon-skill.json`；Plugin 标记：`.mlsmoon-plugin.json`（含 `commit` / `branch`）
- 安装快照（用于列出工作区改动）在 `%AppData%\MlsmoonSkillManager\snapshots\`，不是工作区里的 `.git`

## 引擎适配

当前只支持 **Unity** 和 **Godot**。catalog 用 `engines` 标记，界面显示角标。

- `["all"]` 或缺省：全引擎（现在等于 Unity + Godot）
- `["unity"]` / `["godot"]`：只适配列出的引擎
- 通用资产类（PSD、通用 3D 模型读写）写 `all`
- 引擎专用条目写具体引擎。`spine-gpu-skinning` **只支持 Unity**；随附 Skill 继承 Plugin 的 `engines`
- 还没有 Unreal / 其它引擎，不要写进 catalog

## 主题与界面组件

颜色只放在 `src/MlsmoonSkillManager.App/Themes/{Dark,Light}.xaml`，控件必须用 `DynamicResource`。不要加 WPF-UI NuGet，视觉语言对照公开的 WinUI / WPF-UI Card、Badge、Settings 行。

- 偏好：`Light` / `Dark` / `System`，存在 `UserSettings.Theme`
- `System` 读注册表 `AppsUseLightTheme`，并监听 `SystemEvents.UserPreferenceChanged`
- `ThemeManager` 只替换 Dark/Light 字典，`Themes/Controls.xaml` 始终合并
- 通用控件在 `src/MlsmoonSkillManager.App/Controls/`：`Surface`、`ItemCard`、`FieldRow`、`NavRow`、`Badge`、`StatusLabel`、`TitleBar`、`AppDialog`
- 窗口用 `WindowStyle=None` + `WindowChrome` + 自定义 `TitleBar`，不要系统白底标题栏；深色用 DWM immersive dark mode
- 壳层布局：标题栏 → 当前工作区命令条（在工作区列表上方通栏）→ 左侧工作区 | 右侧卡片 → 底部状态栏（只留末行日志）
- 标题栏右侧：版本、`DEV` 角标、设置齿轮。有应用更新时齿轮上标「新」
- 设置 `AppDialog` 左侧是 Tab：外观、连接、更新、位置、关于、日志。不要再把主题/日志/连接摊在标题栏或主窗口状态栏
- **连接** Tab：GitHub（gh）、局域网、NAS。NAS「网络可达但要密码/密钥」和「完全不可达」必须分开写
- **更新** Tab：对照 GitHub Release 的 `MlsmoonSkillManager-Setup-X.Y.Z.exe`。启动时可自动检查（`UserSettings.AutoCheckUpdates`）。DEV 只提示、不覆盖本机安装
- 日志默认只在状态栏留末行；全文在设置「日志」Tab
- 动效对照 Fluent / WinUI：悬停只做透明度或底色（167ms QuinticEase），不缩放卡片或按钮。按下 83ms、下移 1px。对话框淡入上滑、关闭淡出。用 VisualState，不要叠 EventTrigger 缩放
- 自定义控件必须盖掉 WPF 默认 `ControlBrush`（浅色系统底），样式用 `DynamicResource`，不要露出系统白底
- 新界面先拼这些控件，不要在 `MainWindow.xaml` 再画一套卡片边框
- DEV 角标用 `Badge Appearance="Dev"`（`DevBadgeBrush` / `DevBadgeTextBrush`）

## 单实例

同通道同时只开一个窗口；第二次启动会把已有窗口拉到前台。

- `dev`：`Scripts/rundev.ps1` 或 Debug 构建 / `--dev`
- `exe`：安装包、便携包、Release 构建（无 `--dev`）

两把锁分开，本机可以同时开一个 DEV 和一个已安装 exe。右上角版本号旁边，DEV 必须有醒目 `DEV` 角标。

## 验证

编译能拦住的不要写成测试。改完要验收时读 `.agent/skills/mlsmoon-verify/SKILL.md`，开子 agent，参数用 `-t -build` / `-catalog` / `-install` / `-workspace` / `-gh` / `-ui` / `-release`。

不要 mock `gh`，不要为文案或主题字符串补 xUnit。

## 权限

GitHub 条目用本机 `gh repo view owner/name --json name,visibility,isPrivate`。

- 成功：可安装
- 已登录但 view 失败：界面只保留 **名字**，文案 **当前无权限访问**
- 未安装 / 未登录 gh：同样禁止安装，并说明原因

局域网条目（`source: lan`）不走 gh。公开仓库的格式可以写在 `catalog/skills.override.example.json`。真实覆盖是同目录已被 gitignore 的 `catalog/skills.override.json`（安装后的用户也可用 `%AppData%\MlsmoonSkillManager\skills.override.json`）。公开 `catalog/skills.json` 禁止出现真实 `host` / 内网 IP / NAS 路径。安装包和 `dist/catalog` 不要带上 `skills.override.json`。

探测分两步，host 必须来自 override，代码里不要写死默认 IP：

1. 是否在该网：本机 IPv4 与 `host` 同 /24，或能连上 `host:22`
2. NAS 是否有权：`git ls-remote ssh://{NasUser}@{host}:{gitPath} HEAD`，SSH `BatchMode`，不交互要密码

三种失败必须分开，不要都写成「无权限」：

- **完全不可达**（`OffNetwork` / `Unreachable`）：不在该网，或在网但 `host:22` 不通。卡片：**不在该局域网** / **NAS 不可达**
- **可达但要认证**（`NeedsAuth`）：TCP 22 通，`ls-remote` 报 `Permission denied` / `publickey` / `password`。卡片：**NAS 需要 SSH 密码或密钥**。应用不会弹密码，用户去配密钥或在终端 `ssh`
- **已连通但没仓库权**（`NoPermission`）：认证过了或错误不是密码问题，读不了仓库路径

有权：用 `git archive --remote` 读 `Readme.md` 当卡片说明；默认按工作区 `ProjectSettings/ProjectVersion.txt` 选分支，用户可在卡片上改选。克隆到缓存后复制到 `installPath`（去掉 `.git`），并合并 `Packages/manifest.json`。某个 Unity 大版本若不能装某分支，写在该条目的 override 约定里，不要把内网细节写进公开 catalog。

NAS SSH 用户名存在 `UserSettings.NasUser`，默认本机 Windows 用户名，可在设置「连接」里改。

## 新增 catalog 条目

先确认仓库是 MlsMoon 在 Git 里维护的 skill 或 plugin，再写入 `catalog/skills.json`。

Skill：

```json
{
  "id": "skill-id",
  "name": "skill-id",
  "repo": "https://github.com/MlsMoon/repo",
  "description": "一句话",
  "sourcePath": ".",
  "installName": "skill-id",
  "engines": ["all"]
}
```

仓库根即 skill 时 `sourcePath` 为 `.`，且必须含 `SKILL.md`。Plugin 仓库里的随附 Skill 用相对 `sourcePath`，只写在 `companionSkills`，例如 `Skills~/gpuspine-use-plugin`。

Plugin 放在同文件的 `plugins` 数组（`kind` 会在加载时标成 plugin）：

```json
{
  "id": "spine-gpu-skinning",
  "name": "SpineGpuSkinning",
  "repo": "https://github.com/MlsMoon/SpineGpuSkinning",
  "description": "一句话",
  "sourcePath": ".",
  "installName": "SpineGpuSkinning",
  "installPath": "Assets/Plugins/SpineGpuSkinning",
  "engines": ["unity"],
  "companionSkills": [
    {
      "id": "gpuspine-use-plugin",
      "sourcePath": "Skills~/gpuspine-use-plugin",
      "installName": "gpuspine-use-plugin"
    }
  ]
}
```

Plugin **不要求** `SKILL.md`。公开的 SpineGpuSkinning 本体走 Plugin；`Skills~/gpuspine-use-plugin` 与 `gpuspine-develop-plugin` 只能写在该 Plugin 的 `companionSkills` 里，不要再放进 `skills` 数组。

局域网 Unity 包（不走 GitHub）只放 **用户 override**，示例用占位符，不要填真实地址：

```json
{
  "id": "your-lan-plugin",
  "name": "YourLanPlugin",
  "source": "lan",
  "repo": "ssh://<nas-host>:<git-path>",
  "host": "<nas-host>",
  "gitPath": "<git-path>",
  "installPath": "Packages/YourLanPlugin",
  "engines": ["unity"],
  "branches": [
    { "name": "main", "unity": "6000", "manifest": { "com.example.lan": "file:YourLanPlugin/com.example.lan" } }
  ]
}
```

`branches[].manifest` 按该仓库 Readme 的 manifest 片段维护，同样只写在本机 override。

私有 GitHub 条目和局域网条目都可以只放用户 override，不必写进公开 catalog。

## 打包

版本号在根目录 `VERSION`，格式 `MAJOR.MINOR.PATCH`。

- 用户说发版本 / 更新 / 发布，但没点名升哪一段：只把 **PATCH +1**（`0.2.0` → `0.2.1`）
- **MINOR +1**（`0.2.1` → `0.3.0`，PATCH 归零）必须用户明确说，例如「升 0.1」「升 minor」「0.3.0」
- **MAJOR +1** 同样必须用户明确说

不要自己决定跳 MINOR / MAJOR。改完 `VERSION` 后标签必须是 `v` + 该文件内容。

`release` 分支受保护：禁止 force push / 删除。GitHub 打安装包、发 Release **只**在这种 tag 上跑：

- 标签格式 `vX.Y.Z`，且等于 `VERSION`
- 该 tag 指向的 commit **已经在** `origin/release` 上（`git merge-base --is-ancestor <tag> origin/release`）
- 在 `develop` 上打的 tag **不会**打包

发布顺序（先分支后标签，避免 workflow 跑到时 release 还没有这个 commit）：

1. 在 `develop` 按上面规则改 `VERSION` 并提交
2. 快进合并到 `release`（`git merge --ff-only`），`git push origin release`
3. 在该 commit 上打 `v` + VERSION，`git push origin vX.Y.Z`
4. `.github/workflows/release.yml` 才会编译 `MlsmoonSkillManager-Setup-<version>.exe` 并挂到 GitHub Release。正文来自 `CHANGELOG.md` 的 `## [VERSION]`，不要开 `generate_release_notes`

本地打包：`Scripts\build.bat`，有 Inno Setup 6 时再 `Scripts\build_installer.bat`。`develop` / `release` 的 CI 编过并上传便携 exe，不发 GitHub Release。日常验收用 `-t`，不要靠加单测。

Inno 必须 `DisableDirPage=no`：默认 `auto` 会在同一 `AppId` 已装过时跳过「选择目标位置」。默认目录仍是 `{localappdata}\MlsmoonSkillManager`，升级时预填上次路径，但用户要能改。`PrivilegesRequired=lowest`，选不到需要管理员的 `Program Files`。不要改 `AppId`。

## GitHub Release 与应用内更新

用户说「发版本 / 发 release / 发布」且没点名升哪一段：只 **PATCH +1**，然后按上面顺序推 `release` 再打 tag。不要在 `develop` 上打 tag。

Release 资产名字必须稳定，应用靠这个识别安装包：

- `MlsmoonSkillManager-Setup-{VERSION}.exe`（Inno，应用内「下载并安装」只认名字带 `Setup` 的 exe）
- `MlsmoonSkillManager.exe`（便携，不用于覆盖安装）

应用检查更新：`GET https://api.github.com/repos/MlsMoon/moon-game-dev-tool-manager/releases/latest`（`UpdateService`）。比较 tag `vX.Y.Z` 和本机 `VERSION` / 程序集版本。有新版本就在设置齿轮标「新」，设置「更新」里可下载 Setup 并退出以便覆盖。`/releases/latest` 会跳过 prerelease。

Agent 发版检查清单：

1. `VERSION` 已按规则改并提交在 `develop`
2. `CHANGELOG.md` 已写好 `## [VERSION]`，内容是用户能看懂的条目，不是 compare 链接
3. `git fetch origin release` 后快进合并、`git push origin release`
4. 在该 commit 打 `v` + VERSION，`git push origin vX.Y.Z`
5. 等 `release.yml` 变绿，Release 页能看到 Setup，说明与 changelog 一致
6. 不要 force push `release`，不要改 AppId，不要把未打 tag 的 develop 构建当成正式更新源
7. 不要让 `release.yml` 再开 `generate_release_notes: true`
