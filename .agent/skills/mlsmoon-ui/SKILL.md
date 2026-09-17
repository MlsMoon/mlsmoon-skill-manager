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
- 扫描 / 对照 Git 时 `ItemCard` 整卡是 0–100 进度条（`IsLoading` + `LoadText` + `LoadProgress`）：留下条目名、当前步骤文案和百分比。不要用扫光动画冒充进度，也不要只在卡片正文里写「正在对照」

## 控件

`Controls/`：`Surface`、`ItemCard`、`FieldRow`、`NavRow`、`Badge`、`StatusLabel`、`TitleBar`、`AppDialog`。

圆角只两档，写在 `Themes/Controls.xaml`：`RadiusControl` 2（按钮、输入、勾选、角标、分段），`RadiusSurface` 3（卡片、对话框）。不要胶囊形控件。设置行直接用 `FieldRow`，不要再套一层圆角 Card。

- 窗口用 `WindowStyle=None` + `WindowChrome` + 自定义 `TitleBar`，不要系统白底标题栏；深色用 DWM immersive dark mode
- 壳层：标题栏 → 当前工作区命令条（在工作区列表上方通栏）→ 左侧工作区 | 右侧卡片 → 底部状态栏（只留末行日志）
- 标题栏右侧：版本、`DEV` 角标、设置齿轮。有应用更新时齿轮上标「新」
- DEV 角标用 `Badge Appearance="Dev"`（`DevBadgeBrush` / `DevBadgeTextBrush`）

## 设置

`AppDialog` 左侧是 Tab：外观、连接、更新、位置、关于、日志。不要再把主题/日志/连接摊在标题栏或主窗口状态栏。

- **连接**：见 `mlsmoon-access`。GitHub / 局域网 / NAS
- **更新**：见 `mlsmoon-release`。仅非 DEV。下载进度条。DEV 不出现这个 Tab，也不检查 / 下载 / 覆盖安装，齿轮不标「新」
- Card 宽固定（`CardMinWidth` = `CardMaxWidth`），切 Tab 不能改宽度
- 日志默认只在状态栏留末行；全文在设置「日志」Tab

## 动效

对照 Fluent / WinUI：悬停只做透明度或底色（167ms QuinticEase），不缩放卡片或按钮。按下 83ms、下移 1px。对话框淡入上滑、关闭淡出。用 VisualState，不要叠 EventTrigger 缩放。

## 单实例

同通道同时只开一个窗口；第二次启动会把已有窗口拉到前台。

- `dev`：`Scripts/rundev.ps1` 或 Debug 构建 / `--dev`
- `exe`：安装包、便携包、Release 构建（无 `--dev`）

两把锁分开，本机可以同时开一个 DEV 和一个已安装 exe。右上角版本号旁边，DEV 必须有醒目 `DEV` 角标。

## 不要做

- 为 Theme 字符串、mutex 名字、按钮文案写 Theory
- 悬停放大卡片或按钮
- 切设置 Tab 时改对话框宽度
- 继续往 `MainViewModel.cs` / `MainWindow.xaml` 堆职责；超行先拆再改

改完跑 `-t -ui`：`Scripts\rundev.bat`，看标题栏不是系统白条、右上角是设置齿轮、「当前工作区」在工作区列表上方、设置左侧是 Tab 且切 Tab 宽度不变、连接状态在设置「连接」不在状态栏、DEV 设置里没有「更新」且齿轮无「新」、没开工作区不能安装；卡片/按钮悬停不放大。
