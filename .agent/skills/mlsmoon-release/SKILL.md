---
name: mlsmoon-release
description: 维护 VERSION、Inno Setup、develop/release CI、GitHub Release 与应用内更新。适用于发版、改 installer.iss、release.yml、UpdateService，或用户说发版本 / 发布。
---

# 发版与应用更新

代码：`UpdateService`、`AppChannel`、`VERSION`、`CHANGELOG.md`、`Scripts/installer.iss`、`Scripts/release_notes.ps1`、`.github/workflows/{ci,release}.yml`。

## 版本号

根目录 `VERSION`，格式 `MAJOR.MINOR.PATCH`。

- 用户说发版本 / 更新 / 发布，但没点名升哪一段：只把 **PATCH +1**（`0.2.0` → `0.2.1`）
- **MINOR +1**（`0.2.1` → `0.3.0`，PATCH 归零）必须用户明确说，例如「升 0.1」「升 minor」「0.3.0」
- **MAJOR +1** 同样必须用户明确说

不要自己决定跳 MINOR / MAJOR。改完 `VERSION` 后标签必须是 `v` + 该文件内容。没让发版就不要改 `VERSION`、不要打 tag。

## GitHub 打包

`release` 分支受保护：禁止 force push / 删除。GitHub 打安装包、发 Release **只**在这种 tag 上跑：

- 标签格式 `vX.Y.Z`，且等于 `VERSION`
- 该 tag 指向的 commit **已经在** `origin/release` 上（`git merge-base --is-ancestor <tag> origin/release`）
- 在 `develop` 上打的 tag **不会**打包

发布顺序（先分支后标签，避免 workflow 跑到时 release 还没有这个 commit）：

1. 在 `develop` 按上面规则改 `VERSION` 并提交
2. 快进合并到 `release`（`git merge --ff-only`），`git push origin release`
3. 在该 commit 上打 `v` + VERSION，`git push origin vX.Y.Z`
4. `.github/workflows/release.yml` 才会编译 `MlsmoonSkillManager-Setup-<version>.exe` 并挂到 GitHub Release。正文来自 `CHANGELOG.md` 的 `## [VERSION]`，不要开 `generate_release_notes`

`CHANGELOG.md` 必须有对应 `## [VERSION]`，写用户能看懂的条目，不是 compare 链接。

## 本地打包

- `Scripts\build.bat`；有 Inno Setup 6 时再 `Scripts\build_installer.bat`
- 这两步复制本机整个 `catalog/`，包括已 gitignore 的 `skills.override.json`
- `develop` / `release` 的 CI 编过并上传便携 exe，不发 GitHub Release；CI 工作区没有 override，公开包也就没有
- 日常验收用 `-t`，不要靠加单测

Inno 必须 `DisableDirPage=no`：默认 `auto` 会在同一 `AppId` 已装过时跳过「选择目标位置」。默认目录仍是 `{localappdata}\MlsmoonSkillManager`，升级时预填上次路径，但用户要能改。`PrivilegesRequired=lowest`，选不到需要管理员的 `Program Files`。不要改 `AppId`。

## Release 资产

名字必须稳定，应用靠这个识别安装包：

- `MlsmoonSkillManager-Setup-{VERSION}.exe`（Inno，应用内「下载并安装」只认名字带 `Setup` 的 exe）
- `MlsmoonSkillManager.exe`（便携，不用于覆盖安装）

应用检查更新（**仅非 DEV**）：`GET https://api.github.com/repos/MlsMoon/moon-game-dev-tool-manager/releases/latest`（`UpdateService`）。比较 tag `vX.Y.Z` 和本机 `VERSION` / 程序集版本。有新版本就在设置齿轮标「新」。设置「更新」下载 Setup 时必须显示进度；下完后用 `UpdateService.SilentSetupArgs` 拉起安装包并 `Shutdown`，让 Inno 静默覆盖上次目录。不要再打开交互向导。`/releases/latest` 会跳过 prerelease。

DEV（`rundev` / Debug / `--dev`）关掉整条应用更新：设置没有「更新」Tab，不自动/手动查 GitHub Release，不下载 Setup，不覆盖本机安装，齿轮不标「新」。看版本去「关于」。

## Agent 发版检查清单

1. `VERSION` 已按规则改并提交在 `develop`
2. `CHANGELOG.md` 已写好 `## [VERSION]`
3. `git fetch origin release` 后快进合并、`git push origin release`
4. 在该 commit 打 `v` + VERSION，`git push origin vX.Y.Z`
5. 等 `release.yml` 变绿，Release 页能看到 Setup，说明与 changelog 一致
6. 不要 force push `release`，不要改 AppId，不要把未打 tag 的 develop 构建当成正式更新源
7. 不要让 `release.yml` 再开 `generate_release_notes: true`

改完跑 `-t -release`。界面进度条见 `mlsmoon-ui`。
