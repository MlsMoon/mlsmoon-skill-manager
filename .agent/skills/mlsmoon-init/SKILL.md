---
name: mlsmoon-init
description: 本仓库本机初始化：把 .claude / .grok 的 skills 链到 .agent/skills，并把 CLAUDE.md / GROK.md 链到 AGENTS.md。适用于刚 clone、换机器、链接损坏、用户说初始化 / setup / 给 Claude Grok 做 skill 链接。
---

# 初始化

本仓库维护 skill 的唯一权威目录是 `.agent/skills`。旧名 `.agent` 当成 `.agents`。Claude / Grok 只认自己的根，用**相对路径符号链接**指过去，不要复制 skill 树，也不要在 `.claude` / `.grok` 里另改一份。

`AGENTS.md` 是仓库入口。`CLAUDE.md` 和 `GROK.md` 必须是指向它的文件链接，不要各写一份。

## 先跑

仓库根：

```bat
Scripts\link-skill-roots.bat
```

只检查不改：

```bat
Scripts\link-skill-roots.bat -CheckOnly
```

脚本印 `OK` / `FAIL`。优先 `mklink /D`（目录）和文件符号链接；没权限则目录退到 `mklink /J`，md 退到 `mklink /H`。不要改成拷贝。`.claude/`、`.grok/`、`CLAUDE.md`、`GROK.md` 已 gitignore，换机器跑本脚本重建。

## 要齐什么

| 项 | 怎么齐 |
|---|---|
| `.claude/skills` | 目录符号链接，目标 `..\.agent\skills` |
| `.grok/skills` | 同上 |
| `CLAUDE.md` | 文件符号链接，目标 `AGENTS.md` |
| `GROK.md` | 同上 |

链接坏了只重建，不要把链接改回普通目录或普通文件。新增、修改、删除 skill 只动 `.agent/skills`。

## Agent 步骤

1. 在仓库根跑 `Scripts\link-skill-roots.bat`（用户只要体检则加 `-CheckOnly`）
2. `FAIL` 且提示权限：让用户打开「开发人员模式」或用管理员重跑，禁止 `Copy-Item` skill 树
3. 已有同名普通目录或普通 `CLAUDE.md` / `GROK.md`：停下来问用户，不要删
4. 全部 `OK` 后告诉用户 Claude / Grok 和 Cursor 读的是同一套 skill 与入口 md

不要把本 skill 写进 `catalog/skills.json`。用户没点名提交就不要 commit 这些链接。
