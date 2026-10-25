# T7senaty

أداة صغيرة (C#) لتحسين مشاهدة البث على محاكي **MuMu Player 12** على ويندوز.

A small Windows helper (C#) for watching streams in **MuMu Player 12**.

## What it does (only while the emulator is running)

- High process priority for the emulator and the upscaler
- Hides the mouse cursor after 1 second without movement
- Keeps the screen awake
- Sets the emulator to 1600x900 and hides the FPS counter
- **F11** inside the emulator = full-screen AI-upscaled picture using [Magpie](https://github.com/Blinue/Magpie) (default preset: CuNNy 8x32)
- Tray menu: switch preset, network check
- Runs only from its own shortcut/session, never with Windows; closing the emulator ends it and restores everything
- `backBB/rollback.ps1` restores the original settings and removes the program

## Install (end users)

Download `T7senaty.zip` from **Releases**, extract it fully somewhere permanent, run `تثبيت.bat` once, then use the `T7senaty` shortcut on the Desktop.

## Build from source

```powershell
powershell -ExecutionPolicy Bypass -File src\build.ps1
```

Uses the C# compiler that ships with Windows (no downloads). Optional: put a `T7senaty.ico` in `src\` to use as the icon.
To make a ready-to-share package you also need Magpie v0.13.0 (x64) extracted into `Magpie\` — see `src\build-package.ps1`.

## Credits

- [Magpie](https://github.com/Blinue/Magpie) by Blinue — GPL-3.0. Not included in this repository; the release zip bundles the unmodified v0.13.0 binaries.
- Shader models used through Magpie: CuNNy, FSRCNNX, SSimDownscaler, Deband, CAS (see Magpie for their licenses).

## Notes

- Unsigned executable: Windows SmartScreen may warn on first run.
- Not affiliated with NetEase / MuMu, or Magpie.
