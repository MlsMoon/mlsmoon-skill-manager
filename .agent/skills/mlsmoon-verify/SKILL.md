---
name: mlsmoon-verify
description: 用参数 `-t -xxx` 验证 Moon Game Dev Tool Manager，少写传统单测、不 mock。适用于用户点名 -t / 验证 / 测一下，或安装、catalog、工作区 Git、ui_flow 覆盖到的交互真的变了要验收。收工先在对话里问测试级别，用户不回就按 diff 跑；不要为文案、标题栏、skill 同步默认开子 agent。
---

# 验证（`-t`）

编译能拦住的不要写成测试。需要验收时按参数跑真实命令和真实文件。**不要默认开子 agent，也不要默认 `-all` / `-ui`。**

## 先问级别

收工要跑 `-t` 时，先在对话里写出**建议跑哪几个旗标、为什么**，并问用户要不要改范围。用户已经点名（`-t -ui`、测全部、只编一下）就照做。

**不要停下来干等下一轮。** 这轮没点名，就按建议立刻跑；用户下一句改范围再补跑。

建议怎么选：只写 `-t` 或没点名时按当前 diff；看不出范围就 `-build`（改了 `src/` / `Scripts/` 时至少这个）。安装路径/标记加 `-install`，公开 catalog 加 `-catalog`，工作区列表/Git 状态加 `-workspace`，文件树/冲突再加 `-sync`。`ui_flow` 会点到的路径才加 `-ui`。没点名不要 `-gh` / `-release` / `-all`。

启动扫描、卡片进度条走几轮：`--ui-test` **跳过** gh/git 扫描，所以 `-build` 冒烟和 `-ui` 都看不见。改 `RefreshAsync` / `ScanAllAsync` 时对照代码（只进一次、不要 Access 清掉再 Inspect 从 0%）并用 DEV 窗口看。不要为此加 xUnit，也不要靠 `-ui` 当验收。

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
-t -size
-t -all
```

可叠：`-t -catalog -install`。只写 `-t` 时按当前 diff 选相关项；看不出范围就 `-build -catalog`。

## 原则

- 代码改完、声称完成前，按「先问级别」跑 `Scripts\verify.bat`。没点名至少 `-t -build`。不要开子 agent 才算测过。
- `-t -build` = 编解决方案 **加上** 后台 `--ui-test` 等到 `ui-ready.log`。WPF 的 TwoWay 绑只读属性、窗口构造即崩，编译拦不住，启动冒烟才能拦。
- 不要用 `dotnet build -o %TEMP%` 代替。临时输出不是 rundev 会加载的 exe，也容易在文件锁时假装编过了。输出被占用就如实失败，不要换目录绕开。
- 类型、空引用、XAML 编不过的，到此为止。
- 只验编译和启动冒烟看不出来的：catalog 合并、companion 不当独立 Skill、安装路径/标记、引擎缺省、工作区列表、真 `gh`、按钮流程。全量点选仍是 `-ui`，不要默认叠。
- **禁止 mock**：不要 `IProcessRunner` 假实现、不要假 `gh` 输出、不要为了绿而写 ScriptedRunner。
- **禁止再往 `tests/` 堆 Fact**。已有无 mock 用例只当工具跑，不要加新的。
- 用临时目录，测完删掉。不要碰用户真实工作区，除非用户点名。
- `.htybox/` 不要提交。

## 何时开子 agent

父 agent 先跑 `Scripts\verify.bat -t -build`。编不过或窗口没写出 `ui-ready.log` 就停，不要再开子 agent。

**开子 agent**（只带对得上的 `-t` 旗标，一次一个，禁止顺手加 `-all` / `-ui` / `-gh`）：

- 用户点名 `-t`、验证、测一下
- 安装 / 卸载 / `.mlsmoon` 路由 / 标记 / 路径真的变了 → `-install`
- 公开 `catalog/skills.json` 或 catalog 合并规则变了 → `-catalog`
- 工作区列表、卡片 Git 状态、冲突策略变了 → `-workspace`；对照远端文件树的再加 `-sync`
- **`ui_flow.py` 已经会点到的路径**变了（设置 Tab、连接/位置、打开当前文件夹、卡片「打开目录」、筛选芯片）→ `-ui`

**不要开子 agent**（父 agent 跑过 `-t -build`、对一下 skill 即可）：

- 只改 `.agent/skills`、README、AGENTS、文案（这种可以不跑 `-build`）
- 标题栏角标、DEV「重启」、主题色、Hidden/Collapsed、单个按钮显隐、MainWindow/AppDialog 启动就会绑的控件
- `-size` / `file_budget`：父 agent 自己跑 `Scripts\file_budget.bat`，超行只提示
- diff 只动壳层：仍然要 `-t -build`（含启动冒烟），只是不要开 `-ui` 子 agent

用户没点名就不要 `-gh`（真打 GitHub）、不要 `-release`（没在发版）、不要 `-all`。

## 怎么开子 agent

只有上面「开子 agent」成立时才开 `generalPurpose`。prompt 写死参数和仓库根。父 agent 不要另写一套测试代码。

```
仓库：D:/OtherProjects/mlsmoon-skill-manager
参数：-t <flags>
先读 .agent/skills/mlsmoon-verify/SKILL.md
按参数跑 Scripts/verify.bat -t <flags>（-t -ui 由脚本自己用 Python 开窗口点按钮，子 agent 不必再 rundev）
禁止 mock、禁止新增 xUnit
用临时目录，测完删除
只回报：参数、做了什么、通过/失败、证据（命令输出或截图路径）
```

多旗标叠在**同一个**子 agent。只有用户同时点了 `-ui` 和 `-gh` 才并行两个。

## 各参数做什么

先跑 `Scripts\verify.bat`，它认同样的 `-t -xxx`。

| 参数 | 脚本会做 | 子 agent 还要做 |
|---|---|---|
| `-build` | `dotnet build` 解决方案，再后台开 Debug exe `--ui-test`，等到 `ui-ready.log`（独立 config 目录，不抢 DEV 单实例、不点按钮） | 编不过或进程在 ready 前退出就停。不要 `-o` 临时目录代替，不要跳过启动冒烟 |
| `-catalog` | 读真实 `catalog/skills.json`：id 唯一、companion 不在 `skills`、`spine-gpu-skinning` 只有 unity 且没有 `companionSkills`、psd/3d 为 all、公开清单没有 `source: lan` / 内网 host / `name` / `description`、`installPath` 不含 `..`；`packages` 与 `plugins` 同样校验 | 对照 `mlsmoon-catalog` |
| `-install` | 跑已有 `CatalogAndInstall`（真临时目录、无 mock） | 改了安装/标记/`.mlsmoon` 路由时看失败信息，不要补新 Fact。对照 `mlsmoon-install` |
| `-workspace` | 跑已有 `WorkspaceBook` | 列表迁入/去重/移除是否符合 `mlsmoon-workspace` |
| `-sync` | 用真 `git` 建临时仓库：`ls-remote --heads`、两目录文件对照；断言冲突策略（本地改动 + 远端更新 = 不能自动更新） | 对照 `mlsmoon-workspace` 卡片状态 |
| `-gh` | 对 catalog 里每个 `repo` 跑真 `gh repo view owner/name --json name,visibility,isPrivate` | 没装 gh 或未登录就标 skip，不要伪造 |
| `-ui` | 只跑 `Scripts/ui_flow.py`：Python 自己编译、装 uiautomation、后台拉起 `--ui-test`。Invoke 设置→连接→位置→打开当前文件夹→完成→卡片「打开目录」→已安装 / Skill / Plugin 筛选，写 probe | 脚本失败就停。不要 rundev、不要 `SetActive`/`Click`/`SendKeys`。窗口 AutomationId 是 `MlsmoonUiTest`，不激活、不进任务栏、不抢焦点 |
| `-release` | `VERSION` 是 `X.Y.Z`；`CHANGELOG.md` 有对应 `## [VERSION]`（英文在上、中文在下、`---` 分隔）；`release.yml` 要求 tag 在 `origin/release` 上，用 `release_notes.ps1` 当正文，并上传 `MlsmoonSkillManager-Setup-*.exe` | 没让发版就不要改 `VERSION`、不要打 tag。对照 `mlsmoon-release` |
| `-size` | 跑 `Scripts\file_budget.ps1`：300 行只是提示，超了不失败 | 不要为了行数拆文案或挤表达式。职责混了再拆。对照 `mlsmoon-self-iterate` |
| `-all` | build + catalog + install + workspace + sync + release + size；gh/ui 不自动跑 | 需要再显式加 `-gh` / `-ui` |

## 不要做

- 不要为 Theme 字符串、mutex 名字、按钮文案写 Theory。
- 不要默认开子 agent，不要把 `-ui` / `-gh` / `-all` 顺手叠上去。
- 不要 mock 掉 `gh` 再断言「当前无权限访问」。
- 不要在 CI 之外发明第二套 xUnit 项目。
- 不要把本仓库的维护 skill 写进 `catalog/skills.json`。
