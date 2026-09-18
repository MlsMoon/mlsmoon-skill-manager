# Moon Game Dev Tool Manager

仓库：`MlsMoon/moon-game-dev-tool-manager`。Windows 应用：把 MlsMoon 用 Git 管起来的 Agent Skill、Unity Plugin 和 Package 安装到指定工作区。

- Skill 默认安装根目录：`.agents/skills`。旧目录 `.agent` 当成 `.agents` 的别名，不再单独列为安装目标
- Plugin 默认安装目录：`Assets/Plugins`
- Package 默认安装目录：`Packages`。和 Plugin 同一套安装逻辑，只是目录和分类不同
- 自动检测：`.claude`、`.grok`、`.codex`（旧版 Codex skill），以及已存在的 `.agents`
- 目录 `catalog/skills.json` 是公开清单。覆盖格式写在 `catalog/skills.override.example.json`（可以写公开仓库样例）；真实覆盖是已 gitignore 的 `catalog/skills.override.json`。本地打包按本机 catalog 原样带上 override，不要提交该文件。不在清单和 override 里的本地文件一律不管
- Plugin / Package 的路由 Skill 由仓库 `.mlsmoon/skill.json` 在安装时写入 Skill 根；包内 `Skills~` 不要当成独立 Skill
- GitHub 打安装包只接受 `release` 分支上的 `vX.Y.Z` tag；先推 `release` 再推 tag
- 改某模块前先读 `.agent/skills/mlsmoon-skill-manager/SKILL.md` 的路由表，再只打开对应模块 skill
- 收工读 `.agent/skills/mlsmoon-self-iterate/SKILL.md`：同步 skill。单文件 300 行只是提示，不要为凑行数挤代码或拆文案；职责混了再拆
- 验收读 `.agent/skills/mlsmoon-verify/SKILL.md`：改了代码先跑 `Scripts\verify.bat -t -build`。不要默认开子 agent，不要默认 `-all` / `-ui`。不要堆传统单测，不要 mock
