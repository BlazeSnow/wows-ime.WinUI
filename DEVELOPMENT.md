# 开发指南

本文是 wows-ime 的详细开发指南，描述程序的业务逻辑、数据持久化、架构与构建发布流程。本文鼓励随开发进度同步修改：当业务逻辑、架构或流程发生变化时，应同步更新本文。简要的 AI 编程基准约定见 [AGENTS.md](AGENTS.md)（该文件为基准，不允许修改）。

## 程序介绍

本程序是《战舰世界》中文输入法配置文件的修改器（WinUI 3 桌面应用），通过修订游戏配置文件 `ime_config.xml`，让游戏支持更多的中文输入法。最终发布至 Microsoft Store。

## 配置文件示例

仓库根目录的 `ime_config.xml` 是写入游戏的配置文件示例，结构为 `<data>` 根节点下三个语言分组：

| 分组 | Tag |
| --- | --- |
| `<ChineseSimplified>` | `GFxIME_Ch_Simp` |
| `<Japanese>` | `GFxIME_Jp` |
| `<ChineseTraditional>` | `GFxIME_Ch_Trad_Array` |

每个输入法节点包含 `imeName`、`displayName`、`Tag` 三个元素。生成逻辑见 `GameConfigService.BuildConfigDocument`（生成顺序为简体、日文、繁体）。

## 程序逻辑

1. 扫描并列出用户的输入法
2. 由用户勾选要添加的输入法，并选择各个选定输入法的类型（中文简体、繁体或日文）
3. 支持添加自定义游戏路径和自定义输入法
4. 删除自定义游戏路径或自定义输入法前，需要弹窗确认
5. 检查配置文件目录是否已有 `ime_config.xml`：若有，则提示用户覆盖；若无，由用户确认添加

## 游戏目录约定

1. 需要由用户选定游戏根目录
2. 预设三种渠道的默认路径（常量见 `wows-ime.Pages\Views\HomePage.xaml.cs`）：

   | 渠道 | 默认路径 |
   | --- | --- |
   | Steam | `C:\Program Files (x86)\Steam\steamapps\common\World of Warships` |
   | 莱斯塔启动器 | `C:\Games\Korabli` |
   | 360 启动器 | `C:\Games\World_of_Warships_CN360` |

3. 也存在大量非 Steam 下载的用户，因此必须提供选择文件夹功能
4. 用户选择游戏根目录后，需要确认根目录存在游戏 exe 程序：一般为 `WorldOfWarships.exe`，俄罗斯服（莱斯塔）为 `Korabli.exe`（见 `GameConfigService.HasGameExecutable`）
5. 输入法配置文件的目标路径为 `<游戏根目录>\bin\<版本>\res_mods\ime_config.xml`。`bin` 目录下存在多个数字版本目录（如 `8842736`），程序会为所有数字版本目录写入输入法配置文件（见 `GameConfigService.ResolveTargetConfigFiles`）
6. 注意：`8842736` 只是游戏版本示例，不允许以硬编码版本号作为回退方案

## 持久化配置

软件最终发布至 Microsoft Store，本地测试也使用 VS 打包运行，因此按打包应用处理。

1. 简单状态使用 `ApplicationData.Current.LocalSettings`（键常量见 `SettingsPersistence.cs`）：
   - `Settings.SchemaVersion`：当前配置结构版本（当前为 `1`）
   - `Settings.Language`：界面语言模式（`auto` / `zh-Hans` / `zh-Hant` / `ja`）
   - `Game.SelectedPath`：当前选中的游戏根目录
2. 列表数据使用 SQLite（`Microsoft.Data.Sqlite`），数据库文件位于 `ApplicationData.Current.LocalFolder\settings.db`；写入采用 upsert + 全量同步删除
3. 表结构（建表见 `SettingsPersistence.Initialize`）：
   1. `custom_game_paths`：自定义游戏路径，`path` 为主键；字段包括 `display_name`、`path`、`created_at`、`updated_at`
   2. `custom_input_methods`：自定义输入法，`display_name` 为主键；字段包括 `display_name`、`category`、`created_at`、`updated_at`
4. 旧版本曾使用 `ApplicationData.Current.LocalFolder\config.json`；启动时如果发现该文件，需要迁移到新存储方式
5. 旧 `config.json` 迁移成功后，应重命名为 `config.json.migrated`，不使用 LocalSettings 记录迁移标记
6. 迁移旧数据时：
   1. `SelectedGamePath` 或 `GameDir` 迁移到 `Game.SelectedPath`
   2. `GamePaths` 迁移到 `custom_game_paths`
   3. `Ime` 迁移到 `custom_input_methods`
7. 所有持久化异常均静默吞掉，不影响 UI

## 程序架构

程序使用 WinUI 3（Windows App SDK）架构。解决方案 `wows-ime.slnx` 包含四个项目：

| 项目 | 职责 |
| --- | --- |
| `wows-ime.csproj`（主项目） | 应用入口与组合根：`App.xaml(.cs)`、`PageHost.cs` |
| `wows-ime.Core` | 无 UI 的核心逻辑：模型、接口、输入法扫描/持久化/游戏配置服务 |
| `wows-ime.Pages` | 页面层：Shell / HomePage / SettingsPage 及绑定模型 |
| `wows-ime.Tests` | xUnit 单元测试：Core 层逻辑测试与 TSF 扫描冒烟测试 |

注意：Core 与 Pages 两个子项目物理上位于主项目目录内，主项目 csproj 通过 `Compile Remove`、`Page Remove` 等排除了这些子目录，避免重复编译。

### 主项目（根目录）

- `App.xaml`：资源字典，主题色 `Primary` 与样式 `MyLabel`、`Action`、`PrimaryAction`
- `App.xaml.cs`：`App : Application`。创建主窗口（Mica 背景、标题栏深色模式适配、系统主题监听），调用 `settings.ApplyLanguageMode()` 与 `settings.Initialize()`，挂接全局异常钩子（崩溃日志写入 `ApplicationData.Current.LocalFolder\crash.log`，失败时回退 `%LOCALAPPDATA%\wows-ime\crash.log`）
- `PageHost.cs`：服务组合根/适配器，实现 `IPageHost`、`IPageConfiguration`、`IPageLocalization`、`IPageWindow`、`IPageApplication`，把 Core 服务与 WinUI 平台能力（FolderPicker、Launcher、应用版本、语言覆盖、进程重启）桥接给 Pages

### wows-ime.Core

- `Abstractions/`：`IInputMethodScanner`、`ISettingsRepository`、`ISystemLanguagePreferences`
- `Models/`：`ImeCategory`（枚举：ChineseSimplified / ChineseTraditional / Japanese）、`InputMethodDefinition`、`InputMethodScanResult`、`ScannedImeCandidate`、`PersistedGamePath`、`PersistedInputMethod`
- `Rules/LanguageRules.cs`：语言模式常量（`auto` / `zh-Hans` / `zh-Hant` / `ja`）与解析逻辑（auto 模式按系统语言推断，回退 zh-Hans）
- `Infrastructure/InputMethodScanner.cs`：TSF/COM 输入法扫描。通过 vtable 直接调用 `ITfInputProcessorProfiles`（CLSID `33C53A50-F456-4884-B049-85FD643ECFED`），扫描中文（0x0804/0x0404/0x0C04/0x1004/0x1404）与日语（0x0411）语言 ID 下已启用的输入法配置文件，按名称关键词（速成/倉頡/注音/Quick/Cangjie → 繁体；拼音/五笔 → 简体；Japanese/日文 → 日语）推断分类，并过滤"输入体验/Input Experience"噪音项；失败时返回 `Tsf/*` 警告码
- `Infrastructure/SettingsPersistence.cs`：LocalSettings、SQLite 和旧配置迁移逻辑（见"持久化配置"）
- `Infrastructure/SystemLanguagePreferences.cs`：读取 `GlobalizationPreferences.Languages`
- `Services/GameConfigService.cs`：游戏目录校验、目标配置路径解析、`ime_config.xml` 生成与写入（`WriteConfigFilesAsync` 以 UTF-8 无 BOM 异步写入）

### wows-ime.Pages

- `Abstractions/IPageHost.cs`：页面层依赖接口（配置、本地化、窗口、应用能力）
- `Models/InputMethodItem.cs`：输入法列表绑定模型（`DependencyObject`）
- `Models/GamePathOption.cs`：游戏路径列表绑定模型
- `Views/Shell.xaml(.cs)`：TitleBar + NavigationView + ContentFrame 外壳
- `Views/HomePage.xaml(.cs)`：主界面，三步向导：① 游戏路径（预设单选 + 自定义路径增删）→ ② 输入法选择（列表 + 分类下拉 + 自定义输入法增删 + 刷新）→ ③ 确认写入（步骤进度与状态提示）
- `Views/SettingsPage.xaml(.cs)`：设置页：界面语言切换（切换后弹重启确认对话框）、项目官网/仓库卡片、版本卡片

## 单元测试

测试项目为 `wows-ime.Tests`（xUnit v3，目标框架与 Core 一致），引用 `wows-ime.Core`，随解决方案一起构建。仓库通过 `global.json` 声明 `Microsoft.Testing.Platform` runner 以启用 `dotnet test` 的 MTP 模式（.NET 10 SDK 上 MTP 测试项目不再支持经 VSTest 目标运行）。运行命令：

```powershell
dotnet test --project wows-ime.Tests/wows-ime.Tests.csproj
```

- 覆盖范围：
  - `Services/GameConfigServiceTests`：游戏 exe 校验与参数守卫、数字版本目录解析、配置文档分组与元素顺序、UTF-8 无 BOM 写入、取消令牌行为；使用临时目录并在测试结束时清理
  - `Rules/LanguageRulesTests`：语言模式归一化与校验（区分大小写）、显式模式原样返回、auto 模式按系统语言推断各变体与回退逻辑（`ISystemLanguagePreferences` 使用假实现）
  - `Infrastructure/InputMethodScannerTests`：TSF 扫描冒烟测试，仅断言结果结构与已知警告码（扫描结果依赖真实系统输入法配置）
- `SettingsPersistence` 依赖 `ApplicationData.Current`（打包应用环境），不在单元测试覆盖范围内

## 本地开发

环境要求：Windows 10 17763 及以上、.NET 10 SDK、Visual Studio（含 WinUI 3 与单项目 MSIX 打包支持）；Windows App SDK 2.4.0 与 `Microsoft.Data.Sqlite` 由 NuGet 自动还原。

- 解决方案文件为 `wows-ime.slnx`（XML 格式），平台为 ARM64 / x86 / x64；主项目、Core、Pages 声明相同的平台列表并随解决方案 CPU 分平台构建（输出目录含平台名），Tests 固定 AnyCPU（纯托管逻辑，无需分架构）
- `Properties\launchSettings.json` 提供两个启动配置：`wows-ime (Package)`（以 MSIX 包运行，行为与发布一致，推荐）和 `wows-ime (Unpackaged)`
- 命令行构建示例：

  ```powershell
  msbuild /t:build /restore /p:Configuration=Release /p:Platform=x64
  ```

- 发布（自包含 + full 裁剪，输出到 `bin\<Configuration>\net10.0-windows10.0.26100.0\<RID>\publish\`）：

  ```powershell
  dotnet publish -c Release -r win-x64
  ```

## 打包与发布

1. 单项目 MSIX 打包（`WindowsPackageType=MSIX` + `EnableMsixTooling=true`），发布时生成 x86 / x64 / ARM64 三架构 bundle
2. 启用裁剪：`PublishTrimmed=true`、`TrimMode=full`、`SuppressTrimAnalysisWarnings=false`；`ILLink.Descriptors.xml` 通过 `TrimmerRootDescriptor` 保留 `WinRT.Runtime` 全部成员（修复剪裁导致的启动异常，勿删）
3. 不做本地签名（`AppxPackageSigningEnabled=False`），签名交给 Microsoft Store
4. 版本号位于 `Package.appxmanifest` 的 `Identity/@Version`
5. 应用名称：`Properties/DisplayName` 指向 `ms-resource:AppDisplayName`，文案在 `Strings/*/Resources.resw`（`AppDisplayName` 与 `App/Title` 保持一致）；商店列表的「产品名称」是 Partner Center 保留名称的下拉框，改名须先在 Partner Center 保留新名称，再上传使用新名称的包并在提交时选中它（保留名称见 `Package.StoreAssociation.xml` 的 `ReservedNames`）
6. 商店政策 10.1.1：标题不得以所参照软件/服务的名称开头；政策 10.2.4：依赖游戏运行的工具须在描述开头披露依赖关系
7. 发布流程：
   1. 更新 `Package.appxmanifest` 版本号与官网更新日志（<https://wows.blazesnow.com/CHANGELOG>；仓库内 `CHANGELOG.md` 仅作指引）
   2. 运行根目录 `tag.ps1`：读取 manifest 版本生成 `v<版本>` tag，确认后推送
   3. 推送 tag 触发 `.github/workflows/version.yml` 自动创建 GitHub Release；MSIX 包由本地构建后提交至 Microsoft Store（产品 ID `9P0R5GM2PTKW`，商店关联配置见 `Package.StoreAssociation.xml`）
8. Dependabot 每周更新 github-actions 与 nuget 依赖

## 本地化

- 资源文件：`Strings/zh-Hans/Resources.resw`（源语言，`DefaultLanguage` 为 zh-Hans）、`Strings/zh-Hant/Resources.resw`、`Strings/ja/Resources.resw`；三份文件的键必须保持对齐
- 资源键命名约定：`元素名.属性` 或 `分组/键`（如 `Status/ConfigWritten`、`Dialog/AddCustomIme/Title`）
- 生成语言由 `priconfig.default.xml` 限定为 `zh-Hans;zh-Hant;ja`；`priconfig.packaging.xml` 将 Scale / DXFeatureLevel 资源拆分为资源包
- 运行时读取：代码后置通过 `ResourceLoader.GetString`（当前语言）与 `PageHost.GetString(key, language)`（指定语言）手动赋值，XAML 基本不使用 `x:Uid`；新增 UI 文本时需要在三份 resw 中同时添加，并在页面代码后置的本地化方法中赋值
- 界面语言覆盖：`Settings.Language` + `ApplicationLanguages.PrimaryLanguageOverride`，切换语言后重启应用生效（见 `LanguageRules` 与 `SettingsPage`）

## 代码风格

详见 `.editorconfig`，要点：

- 4 空格缩进、CRLF 换行；`Nullable` 全局启用
- 接口 `I` 前缀；类型与非字段成员 PascalCase；未定义私有字段 `_camelCase`
- 不使用 `this.` 限定；不偏好 var；偏好模式匹配、switch 表达式、主构造函数、block-scoped 命名空间、大括号必写
- Allman 大括号风格（所有大括号换行）；修饰符顺序固定（`public`、`private`、`protected`、`internal`、`static`、…、`async`）
- Core 层通过 Abstractions 接口保持可测试设计，单元测试见"单元测试"章节
