---
name: mlsmoon-ui
description: 维护 WPF 主题、通用控件、设置对话框、单实例与动效。适用于改 Themes、Controls、AppDialog、TitleBar、MainWindow、ThemeManager，或窗口壳、设置 Tab、DEV 角标。
---

# 界面

代码：`src/MlsmoonSkillManager.App` 的 `Themes/`、`Controls/`、`Theming/`、`MainWindow.xaml`、`SingleInstance`。壳层状态目前仍集中在 `MainViewModel`（超行，下次动到先拆，见 `mlsmoon-self-iterate`）。

颜色只放在 `Themes/{Dark,Light}.xaml`，控件必须用 `DynamicResource`。不要加 WPF-UI NuGet，视觉语言对照公开的 WinUI / WPF-UI Card、Badge、Settings 行。

## 主题

- 偏好：`Light` / `Dark` / `System`，存在 `UserSettings.Theme`
- `System` 读注册表 `AppsUseLightTheme`，并监听 `SystemEvents.UserPreferenceChanged`
- `ThemeManager` 只替换 Dark/Light 字典，`Themes/Controls.xaml` 始终合并
- 自定义控件必须盖掉 WPF 默认 `ControlBrush`（浅色系统底），不要露出系统白底
- 新界面先拼 `Controls/` 里的控件，不要在 `MainWindow.xaml` 再画一套卡片边框
- `ItemCard` 的 Actions 必须单独一列（Auto），不要和标题/正文抢同一层。叠上去看起来能点，实际点到的是下面的文字，打开目录会没反应。正文列要 `ClipToBounds`。模板里 Actions 的 `ContentPresenter` 必须把 `DataContext` 绑到卡片本身，并把 Actions 收进逻辑树，否则按钮 `CommandParameter="{Binding}"` 是 null：点了 `CanExecute` / `Execute` 直接返回，状态栏也不写日志
- 卡片按钮：先写 `CommandParameter` 再写 `Command`。`CommandParameter` 绑 `ItemCard.DataContext`，不要只写 `{Binding}`。`CanExecute(null)` 不能把按钮永远灰掉（WPF 会在参数就绪前先问一次 null）；行状态用按钮自己的 `IsEnabled`
- ItemCard Actions：↓ Pull / ↑ Push 常驻（**路由卡除外**）。仓库根还没接上 Git（`NeedsAttach`）时多一个 **初始化 Git**，此时 Pull / Push / 分支下拉 `IsEnabled=false`；**打开目录、卸载**照旧。其它 Git 状态不要为了显隐再藏 Pull/Push。点 **初始化 Git** 必须弹出 `AppDialog`（树已和远端一样也要弹），主按钮「按远端覆盖」；`CloseOnPrimary=False`，跑起来时 `AllowDismiss=False`，遮罩/Esc/取消不能中断 `reset --hard`。对话框和卡片都要有 0–100 进度（拉取远端、覆盖同名文件）。进度条 `Value` 必须 `Mode=OneWay`，绑定属性要有 public set：`ProgressBar.Value` 默认 TwoWay，绑只读属性会在窗口加载时直接抛 `InvalidOperationException` 闪退。`InitGitCommand` 不要走 `CanSkillRow` 的 `Busy`：按钮看起来能点时 `RelayCommand.Execute` 不能直接 return。拿不到卡片、没有工作区都要写日志。Plugin / Package 初始化不要因为没勾选 Skill 安装根而静默失败
- 卡片分支下拉：`IsSynchronizedWithCurrentItem="False"`，显示 Git HEAD，不要让 ICollectionView 自动选第一项。用户改到另一条分支时弹出 `AppDialog`「确认切换分支」，正文用 `BadBrush`，主按钮「仍然切换」，取消/遮罩/Esc 必须把下拉改回当前分支。不要静默 checkout
- 路由卡：隐藏 Pull / Push / 初始化 Git / 分支。换成 **从源同步** / **同步到源**，状态是与源一致 / 工作区相对源有 N 个文件不同 / 源目录还不存在。进度步骤写「对照路由源…」，只刷新当前这张路由卡
- 扫描 / 对照 Git 时 `ItemCard` 整卡是 0–100 进度条（`IsLoading` + `LoadText` + `LoadProgress`）：留下条目名、当前步骤文案和百分比。进度只属于**当前这张卡**的步骤（对照路由源、列出分支、fetch、对照文件树…）。还没轮到的卡停在 0%「排队扫描…」，不要把总进度涂到每一张上，也不要看起来像所有仓库在一起扫。不要用扫光动画冒充进度，也不要只在卡片正文里写「正在对照」
- 卡片权限（可访问 / public / 无权限）跟 Kind、引擎一样用 `Badge`，不要在标题右侧挂裸绿色 `StatusLabel`
- Kind 角标：Skill / Plugin / Package / 路由 分开。Package 用 `BadgeAppearance.Package`。路由 Skill 用「路由」，不要再标成「随插件」或独立 Skill

## 控件

`Controls/`：`Surface`、`ItemCard`、`FieldRow`、`NavRow`、`Badge`、`StatusLabel`、`TitleBar`、`AppDialog`。`AppDialog` 主按钮默认执行完就关；初始化 Git 这类要停在对话框里看进度的，设 `CloseOnPrimary=False`，跑起来时 `AllowDismiss=False`（挡住遮罩 / Esc / 关闭，避免中途掐掉 `reset --hard`）。主按钮、取消按钮把 `Command` 绑到模板上，用 `CanExecute` 变灰，不要在 code-behind 里再 Execute 一遍。

圆角只两档，写在 `Themes/Controls.xaml`：`RadiusControl` 2（按钮、输入、勾选、角标、分段），`RadiusSurface` 3（卡片、对话框）。不要胶囊形控件。设置行直接用 `FieldRow`，不要再套一层圆角 Card。

- 窗口用 `WindowStyle=None` + `WindowChrome` + 自定义 `TitleBar`，不要系统白底标题栏；深色用 DWM immersive dark mode
- 壳层：标题栏 → 当前工作区命令条（在工作区列表上方通栏）→ 左侧工作区 | 右侧卡片 → 底部状态栏（只留末行日志）
- 卡片列表上方是 `CatalogFilterBar`：搜索 + 分段筛选（已安装 / 未安装、Skill / Plugin / Package、Unity / Godot / 全引擎）。多组可叠加。随附 / 路由 Skill 仍要父级已安装才出现。`CatalogFilter.Fill` 必须按 `CatalogOrder` 排：独立 Skill，再 Plugin 及其路由/随附，再 Package；Bind 后追加的路由不能留在列表末尾（会看起来挂在 Package 下面）。Kind 筛选跟父级：Plugin 带出其路由，Package 带出其随附，Skill 只出独立 Skill。筛选逻辑在 `CatalogFilter`，不要再往 `MainViewModel` 里堆
- 标题栏右侧：版本、`DEV` 角标、DEV 专用「重启」、设置齿轮。有应用更新时齿轮上标「新」
- DEV 角标用 `Badge Appearance="Dev"`（`DevBadgeBrush` / `DevBadgeTextBrush`）。「重启」只在 DEV 显示（`--ui-test` 不要）。点了先拉起仓库 `Scripts/rundev.bat -WaitPid <本进程>`（会弹出 rundev 控制台），再关当前窗口；脚本等本进程退出后才 `dotnet run --dev`。不要只把当前 exe 再拉起来——那样还是改之前的二进制，没法用来快速验证。找不到 bat 就留下当前窗口并提示。单实例锁随窗口退出释放，不要提前放锁再直接启动 exe

## 设置

`AppDialog` 左侧是 Tab：外观、连接、更新、位置、关于、日志。不要再把主题/日志/连接摊在标题栏或主窗口状态栏。

- **连接**：见 `mlsmoon-access`。GitHub / 局域网 / NAS。NAS 登录用 PasswordBox（不要绑定明文到 settings）。「允许 Push / Commit」两个勾选在这个 Tab
- **更新**：见 `mlsmoon-release`。仅非 DEV。下载进度条。DEV 不出现这个 Tab，也不检查 / 下载 / 覆盖安装，齿轮不标「新」
- Card 宽固定（`CardMinWidth` = `CardMaxWidth`）。未选中的设置页用 `Hidden` 而不是 `Collapsed`，切 Tab 不能改对话框宽高
- 日志默认只在状态栏留末行；全文在设置「日志」Tab

## 动效

对照 Fluent / WinUI：悬停只做透明度或底色（167ms QuinticEase），不缩放卡片或按钮。按下 83ms、下移 1px。对话框淡入上滑、关闭淡出。用 VisualState，不要叠 EventTrigger 缩放。

## 单实例

同通道同时只开一个窗口；第二次启动会把已有窗口拉到前台。

- `dev`：`Scripts/rundev.ps1` 或 Debug 构建 / `--dev`
- `exe`：安装包、便携包、Release 构建（无 `--dev`）

两把锁分开，本机可以同时开一个 DEV 和一个已安装 exe。右上角版本号旁边，DEV 必须有醒目 `DEV` 角标。

用户说「开一个 dev / rundev / 开 DEV」= 跑 `Scripts\rundev.bat`，把本机 Debug 窗口拉起来给人看。不要理解成开 GitHub PR、也不要为此新建或切换 `dev/*` 分支。切分支 / 建分支见 `mlsmoon-access`：用户没点名就不要动。

## 不要做

- 为 Theme 字符串、mutex 名字、按钮文案写 Theory
- 悬停放大卡片或按钮
- 切设置 Tab 时改对话框宽高
- 继续往 `MainViewModel.cs` / `MainWindow.xaml` 堆另一摊职责；已经读不下去再拆，不要为了行数挤代码

`Scripts/ui_flow.py` 是整段流程：自己编译、后台开 `--ui-test`（`MlsmoonUiTest`，不激活、不进任务栏、屏外且透明），只用 InvokePattern 点设置 / 连接 / 位置 / 打开当前文件夹 / 卡片「打开目录」/ 筛选芯片，写 probe。不要 `SetActive`、不要 `Click`、不要 `SendKeys`，以免抢用户焦点。`--ui-test` 跳过 gh/git 扫描，因此 **拦不住**「启动进度条走两轮」。启动进度条只走一轮：Loaded 只进 `ScanAllAsync`，权限和对照共用同一张卡的 overlay，不要 Access 结束清进度再开一轮 Git。看窗口或对照 `RefreshAsync` / `ScanAllAsync`，不要靠 `-t -ui`。卡片「打开目录」绑行上的 `OpenFolderCommand`，不要再绕 Window + `CanExecute(null)`。其它卡片按钮的 `Command` 必须带 `CommandParameter`，且 `CanExecute(null)` 不能把按钮永远灰掉。真开文件夹走 `Shell.Application.Explore`，不要只丢一个没引号的 `explorer.exe` 路径。

这条路径变了才按 `mlsmoon-verify` 开子 agent 跑 `-t -ui`。标题栏角标、DEV 重启、显隐、文案、对话框进度条：父 agent 跑 `Scripts\verify.bat -t -build`（含 `--ui-test` 等到 `ui-ready.log`）即可，不要用临时输出目录代替，也不要开 `-ui` 全流程。`ProgressBar.Value` 默认 TwoWay，绑只读属性会在窗口加载时崩，编译看不出来。
