# Build information

Two player builds, both containing the Launch scene (index 0) and the Lesson scene.
The condition is never built in: the launch screen takes it from `allocation.csv`.

| Build | Conditions | Output | Command |
|---|---|---|---|
| Quest 3 APK | 2, 3 | `Builds/Quest/lesson.apk` | `StudyBuild.BuildQuest` |
| Desktop (Linux x86_64) | 1 | `Builds/Desktop/lesson/lesson.x86_64` | `StudyBuild.BuildDesktop` |

Run a build from the Editor through the live command runner:

    LiveCommands/tools/lc.sh '{"action":"call_static","typeName":"Study.EditorTools.StudyBuild","methodName":"BuildQuest","arg":""}' 3000

## Identity and version

| Item | Value |
|---|---|
| Product name | Qubit Lesson (shown in the Quest library and the desktop window; neutral by design) |
| Company | Qupacabras |
| Package ID | `edu.colostate.qupacabras.qubitlesson` (Quest and desktop) |
| Version | **1.0.0** (`StudyBuild.Version`) |

All of these are set by `StudyBuild.ConfigureIdentity`, which every build runs. The version
is written into every row of every session file (column `app_version`) and into
`session_start app=...`. **Bump `StudyBuild.Version` for every new build that runs
participants**, so the data always says which build produced it.

Each build also writes `BUILD_STAMP.txt` next to its output: version, commit (with
`-dirty` if the study's code, data or scenes had uncommitted changes), time, Unity
version, product and package. Build study builds from a clean, committed tree.

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
| Package | `edu.colostate.qupacabras.qubitlesson` (shown as "Qubit Lesson" in the Quest library, under Unknown Sources) |
| Scripting | IL2CPP, ARM64 |
| Android SDK | min 34, target 34 |
| Refresh rate | 90 Hz requested at start-up (`DisplayRate`); the launch screen shows the actual rate |
| Hand tracking | off (controllers only) |
| MSAA | 4x |
| Data folder | `/sdcard/Android/data/edu.colostate.qupacabras.qubitlesson/files` |

Install: `adb install -r Builds/Quest/lesson.apk` (keeps the data folder). Uninstalling
the app deletes its data folder, including `session_index.csv`: pull the data first.

The old template app (`com.DefaultCompany.VRTemplate`, "My project") is a different app
after the rename. Remove it from the headset so nobody launches it:
`adb uninstall com.DefaultCompany.VRTemplate` (its data folder holds only test sessions).
adb ships with Unity: `~/Unity/Hub/Editor/6000.6.0f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`;
the `tools/` scripts find it there if adb is not on the PATH.

## Desktop settings

| Setting | Value |
|---|---|
| Scripting | Mono (only the Linux Mono module is installed) |
| Display | 1920 x 1080 borderless fullscreen, vsync, 60 Hz expected; checked and logged at start |
| XR | off (no loader, no start-up) |
| Data folder | Linux `~/.config/unity3d/Qupacabras/Qubit Lesson/`; Windows `%USERPROFILE%\AppData\LocalLow\Qupacabras\Qubit Lesson\` |

The study desktop runs Linux for now. A Windows build would need "Windows Build Support
(Mono)" added in Unity Hub; the same settings apply. Dry-run flags (never in a session): `-dev-windowed`, `-dev-mute`.

## Logs

- Quest: `adb logcat -s Unity` (look for `[Display]`, `[Frames]`, `[Session]`, `[Event]`).
- Desktop: `~/.config/unity3d/Qupacabras/Qubit Lesson/Player.log`.

## Known build-time message

The Android build logs one `NullReferenceException` from the OpenXR package's own
project validation (`MetaQuestFeature.cs:539`, a rule that reads the Standalone OpenXR
settings while the target is switching). It comes from the package, not the study code,
and the build succeeds.
