# AT Installer

**[한국어](README.ko.md)** | English

A lightweight Windows installer framework built with WinUI 3<br><br>
<img width="1178" height="589" alt="image" src="https://github.com/user-attachments/assets/e20ab765-88ab-44c0-837c-f40041b209d0" />

## Overview

AT Installer is a modern, lightweight installer framework for Windows applications. It consists of two main components:

1. **Installer Composer** - A GUI tool for creating installation packages
2. **Installer** - The runtime installer that extracts and installs applications

The framework uses WinUI 3 with Mica backdrop for a modern Windows 11 appearance and supports multiple languages (English, Korean, Japanese, Chinese).

## Features

- Modern UI with WinUI 3 interface and Mica backdrop
- Multi-language Support (English, Korean, Japanese, Chinese)
- Simple Package Format using `.atp` (AT Package) format with ZIP compression
- Customizable scripts during install/uninstall
- Multi-Architecture support (x64, ARM64)
- Silent Install via command-line
- Registry Integration for proper Windows uninstaller registration
- Start Menu Integration with automatic shortcut creation
- Self-Extracting Installer creation using 7z SFX

## Project Structure

The solution contains three projects:

### Installer
Runtime installer application that handles:
- Package extraction and installation
- Registry registration
- Start menu shortcut creation
- Multi-language UI

### InstallerComposer
Package creation tool featuring:
- Application manifest configuration
- Icon embedding
- Custom script configuration
- Settings save/load functionality

### InstallerCommons
Shared library containing:
- ZIP compression utilities with progress reporting
- Installation and uninstallation manifest definitions
- Common helper classes

## Getting Started

### Prerequisites

- Windows 10 (1809) or later
- .NET 10.0 SDK
- Visual Studio 2022 with WinUI 3 workload

### Building from Source

1. Clone the repository
2. Open `AT Installer.sln` in Visual Studio 2022
3. Build the solution

### Creating an Installation Package

1. Launch **InstallerComposer**
2. Fill in the application manifest:
   - **Application ID**: Click "Generate" to create a unique GUID
   - **Application Name**: Display name of your application
   - **Application Publisher**: Publisher/Company name
   - **Installation Folder Name**: Folder name in `%AppData%`
3. Set the application icon (PNG format)
4. Select the **Application Root Directory** containing your application files
5. Choose the main **Executable File**
6. (Optional) Configure post-install/uninstall scripts
7. Set the **Package File Path** for the output `.atp` file
8. Go to **File > Export Package**

### Installing an Application

Double-click the `.atp` file to launch the installer, or run via command line:

```
Installer.exe "path\to\package.atp"
```

Silent installation:
```
Installer.exe "path\to\package.atp" /silent
```

### Silent Installation Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |
| 24 | Cannot downgrade (installed version is newer) |
| 25 | Extraction failed |
| 26 | Registry registration failed |

## Package Format (.atp)

The `.atp` package is a ZIP archive containing:
- `manifest.json`: Installation manifest
- `data.bin`: Compressed application files

## Configuration Files

### Composer Config (.aticconfig)

Save and load package configurations for repeated builds:
- **File > Save Settings**
- **File > Load Settings**

Automate package creation by passing the config file as a command-line argument:
```
InstallerComposer.exe "path\to\config.aticconfig"
```

## Downloads

Download the latest release from the [Releases](https://github.com/airtaxi/AT-Installer/releases) page.

### Available Downloads

The release includes a compressed archive (`Release.7z`) containing tools to create self-extracting installers.

> **Note**: Currently supports **x64** and **ARM64** architectures. x86 support will be added in a future release. If you need x86 right now, the Installer project itself supports x86 - you can build it from source and use the output.

## Creating Self-Extracting Installers

The Release folder includes tools to create self-extracting (SFX) installers that bundle the AT Installer with your `.atp` package into a single `.exe` file.

### Release Folder Structure

```
Release/
├── Archive/                    # x64 Installer build
│   ├── Installer.exe
│   └── (other runtime files)
├── Archive-arm64/              # ARM64 Installer build
│   ├── Installer.exe
│   └── (other runtime files)
├── 7zS.sfx                     # 7-Zip SFX module
├── bz.exe                      # Bandizip command-line tool (optional)
├── config.txt                  # Normal installer config
├── config_silent.txt           # Silent installer config
├── createArchive.bat           # Create Archive.7z from Archive folder
├── createArchive-arm64.bat     # Create Archive-arm64.7z from Archive-arm64 folder
├── composeInstaller.bat        # Create Installer.exe (x64)
├── composeInstaller-arm64.bat  # Create Installer-arm64.exe
├── composeSilentInstaller.bat  # Create silent Installer.exe (x64)
├── composeSilentInstaller-arm64.bat  # Create silent Installer.exe (ARM64)
├── composeByName.bat           # Create custom-named installer (x64)
└── composeByName-arm64.bat     # Create custom-named installer (ARM64)
```

> **Note**: The `Archive` and `Archive-arm64` folders contain the published Installer runtime only. InstallerComposer is a separate development tool and not included in these folders.

> **Tip**: If you have Bandizip installed on your system, you can delete `bz.exe` as the batch files can use the system-installed Bandizip command-line tool instead.

### Step-by-Step Guide

#### 1. Download and Extract Release Archive

Download `Release.7z` from GitHub Releases and extract it to a folder (e.g., `C:\ATInstaller\Release\`)

#### 2. Prepare Your Package

Place your `.atp` package file in the appropriate folder and rename it to `Package.atp`:
- For x64: Copy to `Release\Archive\Package.atp`
- For ARM64: Copy to `Release\Archive-arm64\Package.atp`

> **Important**: The file must be named exactly `Package.atp`

#### 3. Create 7z Archive

Run the appropriate batch file to create the compressed archive:

```batch
# For x64
createArchive.bat

# For ARM64
createArchive-arm64.bat
```

This creates `Archive.7z` or `Archive-arm64.7z` in the Release folder.

> **Note**: These batch files use `bz.exe` (Bandizip CLI) or the system-installed Bandizip to create the 7z archive.

#### 4. Create Self-Extracting Installer

Choose one of the following options:

**Option A: Standard Installer**
```batch
# For x64
composeInstaller.bat

# For ARM64
composeInstaller-arm64.bat
```
Creates `Installer.exe` that shows the installation UI.

**Option B: Silent Installer**
```batch
# For x64
composeSilentInstaller.bat

# For ARM64
composeSilentInstaller-arm64.bat
```
Creates `Installer.exe` that runs in silent mode (no UI).

**Option C: Custom Named Installer**
```batch
# For x64
composeByName.bat

# For ARM64
composeByName-arm64.bat
```
Prompts for a custom filename and creates `YourName.exe`.

#### 5. Distribute

The generated `.exe` file is a standalone installer that:
1. Extracts the bundled files to a temporary location
2. Launches `Installer.exe` with `Package.atp`
3. Installs your application

### Config Files Explained

**config.txt** (Normal installation):
```
;!@Install@!UTF-8!
RunProgram="Installer.exe Package.atp"
;!@InstallEnd@!
```

**config_silent.txt** (Silent installation):
```
;!@Install@!UTF-8!
RunProgram="Installer.exe Package.atp /silent"
;!@InstallEnd@!
```

These config files tell the 7z SFX module what command to run after extraction.

### Example Workflow

```batch
# 1. Extract Release.7z
7z x Release.7z -oC:\ATInstaller\Release

# 2. Copy your package
copy MyApp.atp C:\ATInstaller\Release\Archive\Package.atp

# 3. Create archive
cd C:\ATInstaller\Release
createArchive.bat

# 4. Create installer
composeInstaller.bat

# 5. Distribute Installer.exe to your users
```

## Technical Details

- **Framework**: .NET 10.0
- **UI Framework**: WinUI 3 (Windows App SDK 1.8)
- **Compression**: ZIP with optimal compression
- **Installation Location**: `%AppData%\{InstallationFolderName}`
- **Registry**: `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`
- **SFX Module**: 7-Zip SFX (7zS.sfx)
- **Archive Tool**: Bandizip CLI (bz.exe) or system-installed Bandizip
- **Supported Architectures**: x64, ARM64 (x86 support coming soon)

## Author

**Howon Lee (airtaxi)**

- GitHub: [@airtaxi](https://github.com/airtaxi)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
