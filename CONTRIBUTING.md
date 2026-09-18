# Contributing

1. 从 `develop` 开分支。`release` 受保护，禁止 force push / 删除；只快进合并，不要改写历史。
2. 只把 MlsMoon 公开仓库写进 `catalog/skills.json`。覆盖格式写 `catalog/skills.override.example.json`；真实覆盖（含局域网 host）写已 gitignore 的 `catalog/skills.override.json`。Plugin 走 `plugins`，Package 走 `packages`。Plugin 宿主入口用仓库 `.mlsmoon/skill.json` 生成路由 Skill，不要把包内 `Skills~` 再放进 `skills`。
3. 不要为编译能拦住的问题写测试，也不要 mock。验收门槛见 `.agent/skills/mlsmoon-verify/SKILL.md`：改了代码先跑 `Scripts\verify.bat -t -build`（含窗口启动冒烟）。不要默认开子 agent，不要默认 `-all` / `-ui`。
4. 单文件大约 300 行是可读性提示，不是硬卡。不要为了行数挤表达式或拆文案。文件已经混了好几件事、读不下去时再按模块拆。提示表在 `.agent/skills/mlsmoon-self-iterate/file-budget.json`。`Scripts` 里每条 `.ps1` / `ui_flow.py` 都要有同名 `.bat`，转发全部参数。
5. 发布前只改根目录 `VERSION`。先把 commit 快进推到 `release`，再打并推 `v` + 该版本号；只有 `release` 上的 tag 才会 GitHub 打包。
6. 本地验证：`Scripts\build.ps1`，有 Inno Setup 时再跑 `Scripts\build_installer.ps1`。
