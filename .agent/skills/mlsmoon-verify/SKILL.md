---
name: mlsmoon-verify
description: 用子 agent + 参数 `-t -xxx` 验证 Moon Game Dev Tool Manager，少写传统单测、不 mock。适用于用户写 -t、-t -catalog、-t -install、验证、测一下，或改完 catalog / 安装 / 权限 / UI 之后要验收。
---

# 验证（`-t`）

编译能拦住的不要写成测试。日常验收开子 agent，按参数跑真实命令和真实文件。

```
-t
-t -build
-t -catalog
-t -install
-t -workspace
-t -sync
-t -gh
-t -ui
-t -release
-t -all
```

可叠：`-t -catalog -install`。只写 `-t` 时按当前 diff 选相关项；看不出范围就 `-build -catalog`。

## 原则

- 先 `dotnet build`。类型、空引用、XAML 编不过的，到此为止。
- 只验编译看不出来的：catalog 合并、companion 不当独立 Skill、安装路径/标记、引擎缺省、工作区列表、真 `gh`、窗口壳。
- **禁止 mock**：不要 `IProcessRunner` 假实现、不要假 `gh` 输出、不要为了绿而写 ScriptedRunner。
- **禁止再往 `tests/` 堆 Fact**。已有无 mock 用例只当工具跑，不要加新的。
- 用临时目录，测完删掉。不要碰用户真实工作区，除非用户点名。
- `.htybox/` 不要提交。

## 怎么开子 agent

读完本 skill 立刻开一个 `generalPurpose` 子 agent，prompt 里写死参数和仓库根目录。父 agent 不要自己再写一套测试代码。

```
仓库：D:/OtherProjects/mlsmoon-skill-manager
参数：-t <flags>
先读 .agent/skills/mlsmoon-verify/SKILL.md
按参数跑 Scripts/verify.bat -t <flags>（UI 场景再按 skill 补 rundev）
禁止 mock、禁止新增 xUnit
用临时目录，测完删除
只回报：参数、做了什么、通过/失败、证据（命令输出或截图路径）
```

多参数开一个子 agent。`-t -ui` 和 `-t -gh` 可以并行各开一个。

## 各参数做什么

先跑 `Scripts\verify.bat`，它认同样的 `-t -xxx`。

| 参数 | 脚本会做 | 子 agent 还要做 |
|---|---|---|
| `-build` | `dotnet build` 解决方案 | 编不过就停 |
| `-catalog` | 读真实 `catalog/skills.json`：id 唯一、companion 不在 `skills`、`spine-gpu-skinning` 只有 unity、psd/3d 为 all、公开清单没有 `source: lan` / 内网 host、`installPath` 不含 `..` | 对照 `SKILL.md`「新增 catalog 条目」 |
| `-install` | 跑已有 `CatalogAndInstall`（真临时目录、无 mock） | 改了安装/标记/companion 时看失败信息，不要补新 Fact |
| `-workspace` | 跑已有 `WorkspaceBook` | 列表迁入/去重/移除是否符合 skill |
| `-sync` | 用真 `git` 建临时仓库：`ls-remote --heads`、两目录文件对照；断言冲突策略（本地改动 + 远端更新 = 不能自动更新） | 对照 `SKILL.md` 工作区 Git 约定 |
| `-gh` | 对 catalog 里每个 `repo` 跑真 `gh repo view owner/name --json name,visibility,isPrivate` | 没装 gh 或未登录就标 skip，不要伪造 |
| `-ui` | 只检查工程能编过 | `Scripts\rundev.bat`，看标题栏不是系统白条、右上角是设置齿轮、「当前工作区」在工作区列表上方、设置左侧是 Tab、连接状态在设置「连接」不在状态栏、没开工作区不能安装；卡片/按钮悬停不放大 |
| `-release` | `VERSION` 是 `X.Y.Z`；`CHANGELOG.md` 有对应 `## [VERSION]`；`release.yml` 要求 tag 在 `origin/release` 上，用 `release_notes.ps1` 当正文，并上传 `MlsmoonSkillManager-Setup-*.exe` | 没让发版就不要改 `VERSION`、不要打 tag。应用内更新只认 Setup 资产。Release 不要只显示 Full Changelog 对比链接 |
| `-all` | build + catalog + install + workspace + sync + release；gh/ui 不自动跑 | 需要再显式加 `-gh` / `-ui` |

## 不要做

- 不要为 Theme 字符串、mutex 名字、按钮文案写 Theory。
- 不要 mock 掉 `gh` 再断言「当前无权限访问」。
- 不要在 CI 之外发明第二套 xUnit 项目。
- 不要把本仓库的维护 skill 写进 `catalog/skills.json`。
