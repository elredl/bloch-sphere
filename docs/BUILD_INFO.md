# Build information

Two player builds, both containing the Launch scene (index 0) and the Lesson scene.
The condition is never built in: the launch screen takes it from `allocation.csv`.

| Build | Conditions | Output | Command |
|---|---|---|---|
| Quest 3 APK | 2, 3 | `Builds/Quest/lesson.apk` | `StudyBuild.BuildQuest` |
| Desktop (Linux x86_64) | 1 | `Builds/Desktop/lesson/lesson.x86_64` | `StudyBuild.BuildDesktop` |

Run a build from the Editor through the live command runner:

    LiveCommands/tools/lc.sh '{"action":"call_static","typeName":"Study.EditorTools.StudyBuild","methodName":"BuildQuest","arg":""}' 3000

Each build writes `BUILD_STAMP.txt` next to its output (version, commit, time, Unity
version, package). The version is `1.0+<commit>`, with `-dirty` if the working tree had
uncommitted changes, and every session file records it (`session_start app=...`).
**Build study builds from a clean, committed tree** so the version names exactly the
code that ran.

Before building after any change to the scene builders, rebuild the scenes:
`StyleLabSetup.BuildAll` (fonts, palette, Launch and Lesson scenes, build scene list,
hand tracking off, desktop XR off).

## Toolchain (frozen)

| Item | Value |
|---|---|
| Unity | 6000.6.0f1 |
| Render pipeline | URP 17.6.0; post-processing off on every camera (the build checks) |
| XR | OpenXR 1.18.0 with Meta Quest support, Meta OpenXR 2.6.1 (Display Utilities on), XR Management 4.7.0 |
| Interaction | XR Interaction Toolkit 3.6.0 (starter assets 3.5.1), Input System 1.20.0 |
| Text | TextMeshPro; Inter, Orbitron, Noto Sans Math (dynamic SDF) |

## Quest settings

| Setting | Value |
|---|---|
| Package | `com.DefaultCompany.VRTemplate` (shown as "My project" in the Quest library, under Unknown Sources) |
| Scripting | IL2CPP, ARM64 |
| Android SDK | min 34, target 34 |
| Refresh rate | 90 Hz requested at start-up (`DisplayRate`); the launch screen shows the actual rate |
| Hand tracking | off (controllers only) |
| MSAA | 4x |
| Data folder | `/sdcard/Android/data/com.DefaultCompany.VRTemplate/files` |

Install: `adb install -r Builds/Quest/lesson.apk` (keeps the data folder). Uninstalling
the app deletes its data folder, including `session_index.csv`: pull the data first.

## Desktop settings

| Setting | Value |
|---|---|
| Scripting | Mono (only the Linux Mono module is installed) |
| Display | 1920 x 1080 borderless fullscreen, vsync, 60 Hz expected; checked and logged at start |
| XR | off (no loader, no start-up) |
| Data folder | Linux `~/.config/unity3d/DefaultCompany/My project/`; Windows `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My project\` |

A Windows build needs "Windows Build Support (Mono)" added in Unity Hub; the same
settings apply. Dry-run flags (never in a session): `-dev-windowed`, `-dev-mute`.

## Logs

- Quest: `adb logcat -s Unity` (look for `[Display]`, `[Frames]`, `[Session]`, `[Event]`).
- Desktop: `~/.config/unity3d/DefaultCompany/My project/Player.log`.

## Known build-time message

The Android build logs one `NullReferenceException` from the OpenXR package's own
project validation (`MetaQuestFeature.cs:539`, a rule that reads the Standalone OpenXR
settings while the target is switching). It comes from the package, not the study code,
and the build succeeds.
