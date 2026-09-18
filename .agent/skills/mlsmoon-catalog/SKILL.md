---
name: mlsmoon-catalog
description: 维护公开 catalog、本机 override、引擎标记，以及新增 Skill / Plugin / Package JSON。适用于改 catalog/skills.json、skills.override.json、engines、companion 是否进 skills 数组，或新增 MlsMoon 托管条目。
---

# Catalog

代码：`CatalogStore`、`SkillDefinition`、`GameEngines`、`ToolKind`。清单在 `catalog/`。

只安装清单（及本机 override）里登记的条目。不在清单和 override 里的本地文件一律不管。真实 NAS 地址禁止写进公开仓库。

## 文件

- `catalog/skills.json`：公开 `skills` + `plugins` + `packages`
- `catalog/skills.override.example.json`：覆盖格式（可用公开仓库样例）
- 真实覆盖：已 gitignore 的 `catalog/skills.override.json`；安装后的用户也可用 `%AppData%\MlsmoonSkillManager\skills.override.json`
- 本地 `Scripts/build.ps1` / 安装包按本机 `catalog/` 原样打包，有 override 就打进去；文件本身不要提交
- GitHub checkout 没有 override，所以 CI / GitHub Release 的包没有内网地址

`CatalogStore` 先读捆绑 `skills.json`，再按 `id` 合并捆绑 override、仓库旁 override。`kind` 在加载时标成 skill / plugin / package；`companionSkills` 展开成 `ToolKind.Companion`，带 `ParentPluginId`（父级可以是 Plugin 或 Package）。

## 引擎

当前只支持 **Unity** 和 **Godot**。catalog 用 `engines` 标记，界面显示角标。

- `["all"]` 或缺省：全引擎（现在等于 Unity + Godot）
- `["unity"]` / `["godot"]`：只适配列出的引擎
- 通用资产类（PSD、通用 3D 模型读写）写 `all`
- 引擎专用条目写具体引擎。`spine-gpu-skinning` **只支持 Unity**；随附 Skill 继承 Plugin 的 `engines`
- 还没有 Unreal / 其它引擎，不要写进 catalog

实现：`GameEngines.Normalize`。空 / `all` / `*` 都当成当前全部已支持引擎。

## 新增条目

先确认仓库是 MlsMoon 在 Git 里维护的 skill 或 plugin，再写入 `catalog/skills.json`。私有 GitHub 条目和局域网条目都可以只放用户 override。

Skill：

```json
{
  "id": "skill-id",
  "name": "skill-id",
  "repo": "https://github.com/MlsMoon/repo",
  "description": "一句话",
  "sourcePath": ".",
  "installName": "skill-id",
  "engines": ["all"]
}
```

仓库根即 skill 时 `sourcePath` 为 `.`，且必须含 `SKILL.md`。Plugin 仓库里的随附 Skill 用相对 `sourcePath`，只写在 `companionSkills`，例如 `Skills~/gpuspine-use-plugin`。

Plugin 放在同文件的 `plugins` 数组：

```json
{
  "id": "spine-gpu-skinning",
  "name": "SpineGpuSkinning",
  "repo": "https://github.com/MlsMoon/SpineGpuSkinning",
  "description": "一句话",
  "sourcePath": ".",
  "installName": "SpineGpuSkinning",
  "installPath": "Assets/Plugins/SpineGpuSkinning",
  "engines": ["unity"],
  "companionSkills": [
    {
      "id": "gpuspine-use-plugin",
      "sourcePath": "Skills~/gpuspine-use-plugin",
      "installName": "gpuspine-use-plugin"
    }
  ]
}
```

Plugin **不要求** `SKILL.md`。公开的 SpineGpuSkinning 本体走 Plugin；`gpuspine-use-plugin` 与 `gpuspine-develop-plugin` 只能写在该 Plugin 的 `companionSkills` 里，不要再放进 `skills` 数组。

Package 放在同文件的 `packages` 数组，和 Plugin 同一套字段（`installPath`、`companionSkills`、局域网 `branches[].manifest`），**不要求** `SKILL.md`。默认装到 `Packages/{installName}`。IGP 的 `UrpPackageIGP` 只写本机 override 的 `packages`，不要放进 `plugins`。

局域网 Unity 包（不走 GitHub）只放 **用户 override** 的 `packages`，示例用占位符，不要填真实地址：

```json
{
  "id": "your-lan-package",
  "name": "YourLanPackage",
  "source": "lan",
  "repo": "ssh://<nas-host>:<git-path>",
  "host": "<nas-host>",
  "gitPath": "<git-path>",
  "installPath": "Packages/YourLanPackage",
  "engines": ["unity"],
  "branches": [
    { "name": "main", "unity": "6000", "manifest": { "com.example.lan": "file:YourLanPackage/com.example.lan" } }
  ]
}
```

`branches[].manifest` 按该仓库 Readme 的 manifest 片段维护，同样只写在本机 override。某个 Unity 大版本若不能装某分支，写在该条目的 override 约定里，不要把内网细节写进公开 catalog。

## 不要做

- 公开 `skills.json` 出现 `source: lan`、真实 `host`、内网 IP、NAS 路径
- companion `id` 再出现在 `skills[]`
- `installPath` 含 `..` 或逃出工作区
- 本仓库维护 skill 写进公开 catalog

安装路径和 companion 行为见 `mlsmoon-install`。探测 NAS 见 `mlsmoon-access`。改完跑 `-t -catalog`。
