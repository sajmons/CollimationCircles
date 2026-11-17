# AGENTS.md

Instructions for AI coding agents working in this repository.

## Project overview

- CollimationCircles is a cross-platform (Windows, macOS, Linux) Avalonia desktop app on .NET 10 (`CollimationCircles\CollimationCircles.csproj`).
- MVVM with CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`) and DI via `Ioc.Default`.
- Image analysis uses OpenCvSharp. Cameras are ASCOM Alpaca cameras or a screen-capture "camera" (a background window capturing an external video program).
- Logging uses NLog.
- Other projects in the repo (`LicenceManager`, `CollimationCirclesFeatures`, ...) are separate tools.

## Localization: never hardcode UI text

All user-visible text must come from language resources. Do not add literal UI strings in C# or XAML.

- Resources live in `CollimationCircles\Resources\Lang\` as `en-US.axaml`, `sl-SI.axaml`, `de-DE.axaml`, `fr-FR.axaml`.
- Every key is `Text.<Name>`. **Any new key must be added to all four files** with a proper translation (English, Slovenian, German, French). Keep the key sets identical.
- In view models (`BaseViewModel` subclasses) and `CollimationHelper`, use `ResSvc.TryGetString("Name")`. The `Text.` prefix is added automatically.
- In classes without `ResSvc`, use `Ioc.Default.GetRequiredService<IResourceService>().TryGetString("Name")`.
- In XAML, use `{DynamicResource Text.Name}`.
- For formatted text, store the format in the resource (`"Exposing {0:F2}/{1:F2}s"`) and call `ResSvc.TryGetString("Name").F(args)` (`F` is in `CollimationCircles.Extensions`).
- `TryGetString` throws on a missing key, so verify keys exist in every language file before finishing.
- Notification and message-box titles and bodies are UI text and must be localized too.

### Resource file rules

- Files are UTF-8 without BOM with CRLF line endings. Use the file-edit tools; do not rewrite them with shell commands.
- Do not start element text with `{` (XAML treats it as a markup extension). Do not use leading or trailing spaces (XAML trims them); compose spacing in code.
- Avoid newlines inside resource text; join lines in code with `"\n"`.
- Text drawn with OpenCV `Cv2.PutText` (Hershey font) is ASCII-only. The `Overlay*` resources are deliberately ASCII in all languages.
- Strings captured once in a constructor do not refresh on a runtime language change. That is accepted.

### Strings that stay literal

- Logger messages (`logger.Info/Trace/Error...`).
- Exception messages that are only caught and logged.
- Protocol/JSON keys, URLs, command-line arguments, internal logic codes (e.g. screw direction `"CW"`, `"CCW"`, `"Fixed"`).
- Language names in the language picker (endonyms) and the "JSON" file-filter label.
- Never branch logic on localized text; compare codes or enums instead.

## Notifications

- Use `INotificationService` (`ShowInfo/ShowSuccess/ShowWarning/ShowError(title, message)`).
- `MainView.axaml.cs` must create the `WindowNotificationManager` and call `NotificationService.Initialize()` **before** `InitializeComponent()`, otherwise the service throws "not initialized" at startup.

## Screen capture

- Screen capture is only available if `ScreenCaptureBackendFactory.IsSupported` is true. Create the screen-capture camera (and treat `selectedCamera` as nullable) only in that case.
- Backends: Windows, macOS, X11, Wayland (portal, with `grim` fallback). Do not assume capture works on every platform.
- When screen capture starts, the user is notified (`ScreenCaptureHintTitle/Message`) to first run an external program showing the camera video and move the window above it.

## Coding conventions

- Follow the existing style: block-scoped namespaces, Allman braces, `NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger()` per class.
- Preserve each file's existing line endings and encoding.
- Comment only code that needs clarification.
- Keep changes surgical; do not fix unrelated issues.
- Documentation (`README.md`) must not mention VLC or UVC cameras; those are no longer supported. Keep the Features list and platform install instructions accurate when behavior changes.

## Build and verification

- Build: `dotnet build CollimationCircles\CollimationCircles.csproj --no-restore`. There is no test project.
- The Visual Studio `run_build` output can show hundreds of misleading `CS0103`/`CS0759` errors when the CommunityToolkit source generator fails on one real error. Run the CLI build to find the real error.
- After adding resource keys, check that all four language files have identical key sets and that every key used in code or XAML exists.
- The PowerShell console may show UTF-8 text as mojibake; check the file contents, not the console output.
- Do not leave temporary files in the repo.

## Token and cost optimization

Keep token traffic low; every tool result and re-read file is billed as input on every later turn.

- **Read narrowly.** Use `grep` first to find the line, then read only that range (`view_range` / `get_file` with start and end lines). Do not read whole large files such as `CollimationAnalysisViewModel.cs`, `StreamViewModel.cs`, `AlpacaCameraClient.cs` or the Lang files.
- **Never re-read what you just edited or already read** unless the content may have changed. Trust a successful edit result.
- **Batch independent calls** (reads, greps, edits) in one turn, and use `multi_replace_string_in_file` for several edits instead of repeated single edits.
- **Filter command output.** Pipe builds and scans through `Select-String` / `Select-Object -First N` and print only errors and the summary line. Never dump full build logs, `git diff` of the whole repo, or whole files to the console.
- **Exclude noise** from searches: `bin`, `obj`, `session-state`, `FocusMaskTestImages`, `Documentation`, and generated files. Use file globs (`*.cs`, `*.axaml`) to narrow scope.
- **Prefer cheap checks.** Use the CLI `dotnet build --no-restore` (small filtered output) over the Visual Studio `run_build`, which can return up to 50 long, misleading errors.
- **Verify key parity with one script** (compare key sets across the four language files) instead of viewing the files.
- **Scope the task.** Decide up front which files are in scope and avoid exploratory reads of unrelated projects (`LicenceManager`, `CollimationCirclesFeatures`, `UseCase`).
- **Do not delegate** small lookups to sub-agents; each sub-agent starts a new context and re-pays for exploration. Delegate only genuinely large, separable work.
- **Keep replies short.** Summarize results in a few lines; do not repeat code or file contents already visible in the diff.
- **Do not repeat failed approaches.** After one failure, change the approach (for example, switch from the IDE build to the CLI build) instead of retrying the same call.
- **Avoid planning/notes files** unless requested, and delete temporary scan outputs immediately after use.
