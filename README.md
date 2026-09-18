# Moon Game Dev Tool Manager

[![CI](https://github.com/MlsMoon/moon-game-dev-tool-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/MlsMoon/moon-game-dev-tool-manager/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/MlsMoon/moon-game-dev-tool-manager)](https://github.com/MlsMoon/moon-game-dev-tool-manager/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Windows 桌面应用：把 **MlsMoon 用 Git 管起来的 Agent Skill、Unity Plugin 和 Package** 装进指定工作区。不在公开清单里的本地文件不会被扫描、改写或删除。

<p align="center">
  <img src="assets/logo.png" alt="Moon Game Dev Tool Manager" width="128" height="128">
</p>

仓库：[`MlsMoon/moon-game-dev-tool-manager`](https://github.com/MlsMoon/moon-game-dev-tool-manager)（旧名 `mlsmoon-skill-manager` 会跳转到这里）。

## 它做什么

1. 左侧添加并切换多个工作区；列表和每个工作区自己的 Skill 安装目标记在本机。旧的 `lastWorkspace` 会自动迁进列表。
2. **Skill** 自动检测 `.claude` / `.grok` / `.codex`（旧版 Codex）。**默认安装目标是 `.agents`**。旧目录 `.agent` 会当成 `.agents`。
3. **Plugin** 默认 `{工作区}/Assets/Plugins/{installName}/`。例如公开仓库 [SpineGpuSkinning](https://github.com/MlsMoon/SpineGpuSkinning) 装到 `Assets/Plugins/SpineGpuSkinning`。
4. **Package** 和 Plugin 同一套安装逻辑，默认 `{工作区}/Packages/{installName}/`。局域网 URP 包写本机 override 的 `packages` 数组，不要放进 `plugins`。
5. 只展示 `catalog/skills.json` 里登记过的条目（`id` ↔ 仓库）。卡片名称和简介从仓库 `SKILL.md` / README 读，并按 commit 缓存在本机。随附 Skill 不能当独立项安装；所属 Plugin / Package 装进当前工作区后才显示。
6. 用本机 [GitHub CLI](https://cli.github.com/)（`gh`）检查仓库权限。
7. 私有仓库如果当前账号看不到：仍然显示 **名字**，并提示 **当前无权限访问**，不能安装。
8. 有权限时：Skill 同步到 `{工作区}/{根}/skills/{id}/`（身份看 `.mlsmoon/skill.json`，文件夹改名也能找到）；Plugin / Package 同步到自己的 `installPath`，并按插件仓 `.mlsmoon` 写入一条路由 Skill。
9. 打开工作区后对照 Git：卡片写清本机落后远端、本机超前、已分叉还是已对齐，并列工作区改过的文件。冲突只提示，不自动覆盖。
10. 右上角设置左侧是 Tab：外观、连接、更新、位置、关于、日志。GitHub / 局域网 / NAS 状态在「连接」里。NAS 网络可达但 SSH 要密码，和完全连不上会分开提示。
11. 设置「更新」对照 [GitHub Releases](https://github.com/MlsMoon/moon-game-dev-tool-manager/releases) 的 Setup 包，可自动检查并下载安装。
12. 局域网包（`source: lan`）写已被 gitignore 的 `catalog/skills.override.json` 的 `packages` 数组，不进公开 `catalog/skills.json`。公开仓库的格式可以写在 [`catalog/skills.override.example.json`](catalog/skills.override.example.json)。有权则读仓库 Readme，可选分支并合并 `Packages/manifest.json`。

## 安装包

从 [GitHub Releases](https://github.com/MlsMoon/moon-game-dev-tool-manager/releases/latest) 下载：

| 文件 | 用途 |
|---|---|
| `MlsmoonSkillManager-Setup-x.y.z.exe` | 推荐。Inno Setup 安装器，向导里可选目录，默认 `%LocalAppData%\MlsmoonSkillManager`，开始菜单是 **Moon Game Dev Tool Manager**，可卸载。不需要管理员。 |
| `MlsmoonSkillManager.exe` | 便携单文件。需要旁边有 `catalog\skills.json`，或把覆盖文件放到 `%AppData%\MlsmoonSkillManager\skills.override.json`。 |

安装包文件名仍带 `MlsmoonSkillManager`，避免已装用户的卸载入口和设置目录对不上。

需要本机已安装并登录 `gh`。私有条目能否出现为“可访问”，完全以当前 `gh auth status` 对应的 GitHub 账号为准。

## 目录映射

内置清单：[`catalog/skills.json`](catalog/skills.json)。

本地覆盖（按 `id` 合并，可新增私有 skill / plugin）：

```
catalog/skills.override.json
```

这份真实覆盖已 gitignore，不要提交。本地 `Scripts\build.bat` / `build_installer.bat` 会按本机 `catalog/` 原样打包（有 override 就带上）。GitHub 上的安装包没有这份文件。公开仓库的格式写在 [`catalog/skills.override.example.json`](catalog/skills.override.example.json)。安装后的用户也可以用 `%AppData%\MlsmoonSkillManager\skills.override.json`。

Skill：

```json
{
  "id": "open-psd-kit",
  "repo": "https://github.com/MlsMoon/open-psd-kit",
  "engines": ["all"]
}
```

Plugin（宿主入口写在插件仓 `.mlsmoon/skill.json`，不要把 `Skills~` 放进 `skills`）：

```json
{
  "id": "spine-gpu-skinning",
  "repo": "https://github.com/MlsMoon/SpineGpuSkinning",
  "installName": "SpineGpuSkinning",
  "installPath": "Assets/Plugins/SpineGpuSkinning",
  "engines": ["unity"]
}
```

`sourcePath` 为 `.` 时，仓库根目录就是要复制的内容。Skill 必须有 `SKILL.md`；Plugin / Package 不要求。

[SpineGpuSkinning](https://github.com/MlsMoon/SpineGpuSkinning) 的本体走 Plugin。安装后写入路由 Skill `spine-gpu-skinning-skill`（角标「路由」），指向插件内 `Skills~/`。插件装进当前工作区后才会显示这条路由 Skill。

## 开发

```bat
Scripts\verify.bat -t -build -catalog
Scripts\rundev.bat
```

验收参数见 `.agent/skills/mlsmoon-verify/SKILL.md`（`-t -install` / `-gh` / `-ui` / `-size` …）。不要为了绿补 mock 单测。维护约定按模块拆在 `.agent/skills/mlsmoon-*`；收工对照 `mlsmoon-self-iterate`。

`rundev` 走 Debug + `--dev`，右上角有橙色 **DEV** 标记。DEV 和安装/便携 exe 各只允许一个实例，但可以同时开各一个。`Scripts` 里每个 `.ps1` 都有同名 `.bat`，双击即可。

打包便携 exe：

```bat
Scripts\build.bat
```

再打 Inno Setup 安装包（需 [Inno Setup 6](https://jrsoftware.org/isinfo.php)）：

```bat
Scripts\build_installer.bat
```

## 分支与 CI

| 分支 | 作用 |
|---|---|
| `develop` | 开发分支。推送后跑测试并上传便携 exe。 |
| `release` | 受保护的发布分支。禁止 force push。 |

只有 **已经在 `release` 上的 commit** 再打 `vX.Y.Z` 标签（必须与根目录 `VERSION` 一致）时，Release workflow 才会打包并发布：

- `MlsmoonSkillManager.exe`
- `MlsmoonSkillManager-Setup-X.Y.Z.exe`

在 `develop` 上打的 tag 不会发 GitHub Release。建议：`develop` 改 `VERSION` → 快进合并并推送 `release` → 再推 tag。

## 工作区约定

安装位置：

```
{workspace}/.agents/skills/{skill-id}/   # 默认；旧 .agent/skills 视为同一目标
{workspace}/.claude/skills/{skill-id}/
{workspace}/.grok/skills/{skill-id}/
{workspace}/.codex/skills/{skill-id}/    # 旧版 Codex skill
{workspace}/Assets/Plugins/{plugin-id}/  # Plugin，可用 installPath 覆盖
{workspace}/Packages/{package-id}/       # Package，可用 installPath 覆盖
```

本工具只会删除带 `.mlsmoon-skill.json`、`.mlsmoon-plugin.json` 或 `.mlsmoon-package.json` 标记的目录，避免误删你自己放的文件。

## License

[MIT](LICENSE) © MlsMoon
