---
name: mlsmoon-access
description: 维护 GitHub gh 权限、局域网探测、NAS SSH 登录与三种失败态。适用于改 GhCli、GitRemote、GitSsh、NasCredentials、LanNetwork、RepoUrl，或卡片写「当前无权限访问 / 不在该局域网 / NAS 需要在设置里登录 SSH」。
---

# 权限与远端

代码：`GhCli`、`GitRemote`、`LanNetwork`、`RepoUrl`、`AccessState`。设置「连接」Tab 的文案也在这里定，不要把连接状态摊回主窗口状态栏。

GitHub 条目用本机 `gh repo view owner/name --json name,visibility,isPrivate`。局域网条目（`source: lan`）不走 gh。host 必须来自 override，代码里不要写死默认 IP。

## GitHub

- 成功：可安装
- 已登录但 view 失败：界面只保留 **名字**，文案 **当前无权限访问**
- 未安装 / 未登录 gh：同样禁止安装，并说明原因
- 不要 mock `gh` 再断言文案

`GhAccountStatus`：`GhMissing` / `GhNotLoggedIn` / 已登录。单条仓库是 `RepoAccess`；`CanInstall` 仅 `Accessible`。

companion 的权限跟所属 Plugin，不要单独 `gh repo view`。

本仓库维护，下列都要用户**点名**才做，默认留在当前分支（通常是 `develop`）改：

- 切分支 / 建分支 / `checkout -b` / 新建 `dev/*`
- 推远程
- `gh pr create` / 开 PR
- 提交

说「开一个 dev」是跑 `Scripts\rundev.bat`，不是切分支、不是建 `dev/*`、不是提 PR。为了提 PR 方便而自己开分支也不行。用户说「合并到 develop」才允许回到 `develop` 并合并。

## 局域网探测

分两步：

1. 是否在该网：本机 IPv4 与 `host` 同 /24，或能连上 `host:22`
2. NAS 是否有权：`git ls-remote ssh://{NasUser}@{host}:{gitPath} HEAD`
   - 设置里**已登录 NAS**：`GitSsh` 用 Windows 凭据管理器里的密码走 `SSH_ASKPASS`，不要 `BatchMode`
   - **未登录**：SSH `BatchMode`，不交互要密码

三种失败必须分开，不要都写成「无权限」：

- **完全不可达**（`OffNetwork` / `Unreachable`）：不在该网，或在网但 `host:22` 不通。卡片：**不在该局域网** / **NAS 不可达**
- **可达但要认证**（`NeedsAuth`）：TCP 22 通，`ls-remote` 报 `Permission denied` / `publickey` / `password`。卡片：**NAS 需要在设置里登录 SSH**。不要在每次 git 时弹密码
- **已连通但没仓库权**（`NoPermission`）：认证过了或错误不是密码问题，读不了仓库路径

有权：用 `git archive --remote` 读仓库 README（或 Skill 的 `SKILL.md`）当卡片名称/简介，结果写进 `%LocalAppData%\MlsmoonSkillManager\cache\catalog-meta.json`，按远端 commit 复用，不要每次探测都重抓。默认按工作区 `ProjectSettings/ProjectVersion.txt` 选分支，用户可在卡片上改选。克隆后安装目录**留下 `.git`**，并合并 `Packages/manifest.json`。

NAS SSH 用户名存在 `UserSettings.NasUser`，填的是 `ssh://用户名@主机` 里的用户名，不是 Windows 用户名。密码只进 Windows 凭据管理器（目标 `MlsmoonSkillManager:nas`），禁止写进 `settings.json`。设置「连接」里登录一次，之后安装 / Pull / Push 自动带这份凭据。退出登录就删凭据。

## 设置连接 Tab

- GitHub（gh）、局域网、NAS 分三条
- NAS 用户 / 密码 / 登录 / 退出
- 「允许 Push」「允许 Commit」默认关。打开 Push 后卡片 ↑ Push 才可点；打开 Commit 后卡片 Commit 才可点。Commit 只提交，不会顺手 Push
- 「网络可达但要密码」和「完全不可达」必须分开写
- 未配置局域网条目时说明：把 `source: lan` 写进本机 `skills.override.json`

## 不要做

- 代码或公开 catalog 写死 NAS IP
- 三种失败合成一句「无权限」
- 把 NAS 密码写进 settings.json / catalog / 仓库
- 每次 git 再弹一次密码（设置里登录一次即可）
- 为权限文案补 xUnit / mock `gh`

清单里 LAN 字段见 `mlsmoon-catalog`。探测逻辑或失败文案变了、且用户要真打 GitHub 时，再按 `mlsmoon-verify` 跑 `-t -gh`（没装 gh 就 skip）。局域网文案对照本文件，不要伪造探测结果。
