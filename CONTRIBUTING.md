# Contributing

1. 从 `develop` 开分支。`release` 受保护，禁止 force push / 删除；只快进合并，不要改写历史。
2. 只把 MlsMoon 公开仓库写进 `catalog/skills.json`。覆盖格式写 `catalog/skills.override.example.json`；真实覆盖（含局域网 host）写已 gitignore 的 `catalog/skills.override.json`。Plugin 走 `plugins`，Package 走 `packages`；随附 Agent skill 写 `companionSkills`，不要再放进 `skills`。
3. 不要为编译能拦住的问题写测试，也不要 mock。验收用 `Scripts\verify.bat -t -xxx` 或按 `.agent/skills/mlsmoon-verify/SKILL.md` 开子 agent。
4. 单文件默认不超过 300 行。超了先按模块拆，不要继续往里加。少数例外和历史债务写在 `.agent/skills/mlsmoon-self-iterate/file-budget.json`。
5. 发布前只改根目录 `VERSION`。先把 commit 快进推到 `release`，再打并推 `v` + 该版本号；只有 `release` 上的 tag 才会 GitHub 打包。
6. 本地验证：`Scripts\build.ps1`，有 Inno Setup 时再跑 `Scripts\build_installer.ps1`。
