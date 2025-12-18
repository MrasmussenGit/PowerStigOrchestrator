# PowerStig Orchestrator

PowerStig Orchestrator is a lightweight launcher for two separate WPF applications:
- PowerStig Converter UI
- MOF Inspector

Important: The two applications are not included in this repository. You must download their x64 Windows executables and place them into an `Apps` folder to use this launcher.

## Download the required apps (x64 Windows)

1. PowerStig Converter UI  
   URL: https://github.com/MrasmussenGit/PowerStigConverterUI/releases  
   - Open the Releases page.
   - In the “Assets” section for the latest release, download the x64 Windows executable.
   - Save the `.exe` into the `Apps` folder (see “Setup” below).

2. MOF Inspector  
   URL: https://github.com/MrasmussenGit/MOFInspector/releases  
   - Open the Releases page.
   - In the “Assets” section for the latest release, download the x64 Windows executable.
   - Save the `.exe` into the `Apps` folder (see “Setup” below).

## Setup

- Create an `Apps` folder next to the launcher executable (or at the repo root).
- Place both downloaded `.exe` files in the `Apps` folder.
- Do not commit binaries to source control.

## How it works

- On startup, the launcher searches for an `Apps` folder by walking up from its executable location and looks for executables that closely match:
  - “PowerStig Converter UI”
  - “MOF Inspector”
- The main window shows three buttons:
  - Launch PowerStig Converter UI
  - Launch MOF Inspector
  - Exit
- If an app is missing, its button is disabled and the launcher shows a friendly error message with the expected path and download instructions.
- When launching, the app displays a progress indicator (“Launching… elapsed mm:ss”) until the target application’s UI is ready or a timeout occurs.

## Building

- Target framework: .NET 9 (`net9.0-windows`)
- WPF: enabled
- Version information is shown at the bottom of the launcher window, sourced from the project’s `<Version>` in `PowerStigOrchestrator.csproj`.

## Good practices

- Keep `Apps/` out of source control.
- If you want to automate local development, consider an MSBuild step or CI workflow to populate `Apps/` from the latest release assets.
