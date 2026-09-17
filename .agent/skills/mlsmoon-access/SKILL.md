---
name: mlsmoon-access
description: 维护 GitHub gh 权限、局域网探测与 NAS SSH 三种失败态。适用于改 GhCli、GitRemote、LanNetwork、RepoUrl，或卡片写「当前无权限访问 / 不在该局域网 / NAS 需要 SSH 密码」。
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

## 局域网探测

分两步：

1. 是否在该网：本机 IPv4 与 `host` 同 /24，或能连上 `host:22`
2. NAS 是否有权：`git ls-remote ssh://{NasUser}@{host}:{gitPath} HEAD`，SSH `BatchMode`，不交互要密码

三种失败必须分开，不要都写成「无权限」：

- **完全不可达**（`OffNetwork` / `Unreachable`）：不在该网，或在网但 `host:22` 不通。卡片：**不在该局域网** / **NAS 不可达**
- **可达但要认证**（`NeedsAuth`）：TCP 22 通，`ls-remote` 报 `Permission denied` / `publickey` / `password`。卡片：**NAS 需要 SSH 密码或密钥**。应用不会弹密码，用户去配密钥或在终端 `ssh`
- **已连通但没仓库权**（`NoPermission`）：认证过了或错误不是密码问题，读不了仓库路径

有权：用 `git archive --remote` 读 `Readme.md` 当卡片说明；默认按工作区 `ProjectSettings/ProjectVersion.txt` 选分支，用户可在卡片上改选。克隆到缓存后复制到 `installPath`（去掉 `.git`），并合并 `Packages/manifest.json`。

NAS SSH 用户名存在 `UserSettings.NasUser`，默认本机 Windows 用户名，可在设置「连接」里改。

## 设置连接 Tab

- GitHub（gh）、局域网、NAS 分三条
- 「网络可达但要密码/密钥」和「完全不可达」必须分开写
- 未配置局域网条目时说明：把 `source: lan` 写进本机 `skills.override.json`

## 不要做

- 代码或公开 catalog 写死 NAS IP
- 三种失败合成一句「无权限」
- 应用里弹 SSH 密码框
- 为权限文案补 xUnit / mock `gh`

清单里 LAN 字段见 `mlsmoon-catalog`。改完跑 `-t -gh`（真 `gh`；没装就 skip）。局域网文案对照本文件，不要伪造探测结果。
