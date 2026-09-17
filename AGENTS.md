# Moon Game Dev Tool Manager

仓库：`MlsMoon/moon-game-dev-tool-manager`。Windows 应用：把 MlsMoon 用 Git 管起来的 Agent Skill 和 Unity Plugin 安装到指定工作区。

- Skill 默认安装根目录：`.agent/skills`
- Plugin 默认安装目录：`Assets/Plugins`
- 自动检测：`.claude`、`.grok`，以及已存在的 `.agents`
- 目录 `catalog/skills.json` 是公开清单。覆盖格式写在 `catalog/skills.override.example.json`（可以写公开仓库样例）；真实覆盖是已 gitignore 的 `catalog/skills.override.json`。不在清单和 override 里的本地文件一律不管
- Plugin 的 `companionSkills`（如 gpuspine-use/develop）随插件安装，不要当成独立 Skill
- GitHub 打安装包只接受 `release` 分支上的 `vX.Y.Z` tag；先推 `release` 再推 tag
- 改 catalog、安装逻辑、Inno Setup 或 GitHub Actions 前，先读 `.agent/skills/mlsmoon-skill-manager/SKILL.md`
- 验收用 `-t -xxx` 开子 agent，读 `.agent/skills/mlsmoon-verify/SKILL.md`。不要堆传统单测，不要 mock
