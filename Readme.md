# Microphone Volume Enforcer

A modern WPF application that monitors and enforces microphone volume levels to ensure consistent audio input across different applications and system changes.

Yes, i had AI improve the readme. i was tired and i had a huge need for a toilet break. Bite me.

## Features

### 🎤 **Audio Control**
- **Real-time Microphone Monitoring**: Automatically detects and lists all active capture devices
- **Volume Enforcement**: Maintains specified volume levels even when other applications try to change them
- **Device Selection**: Choose which microphone to monitor and control
- **Volume Slider**: Set target volume from 0% to 100%
- **Live Volume Updates**: Real-time volume adjustment with enforcement

### 🖥️ **User Interface**
- **Modern Web-Based UI**: Built with WebView2 for a responsive, modern interface
- **Dark/Light Theme**: Toggle between themes with system preference detection
- **Keyboard Shortcut**: Alt+T to quickly toggle theme
- **System Tray Integration**: Minimize to system tray for background operation
- **Balloon Notifications**: Informative notifications when minimizing to tray

### ⚙️ **Settings & Configuration**
- **Auto-Save Settings**: All changes are automatically saved without manual intervention
- **Start with Windows**: Option to launch application on system startup
- **Close Behavior Configuration**: Choose what happens when clicking the X button:
  - Minimize to Tray
  - Ask what to do
  - Exit Application

### 🔧 **System Integration**
- **Windows Registry Integration**: Proper startup configuration through Windows registry

## Installation

### Prerequisites
- Windows 10/11
- .NET 10.0 Runtime
- WebView2 Runtime (usually pre-installed on modern Windows)

### Running from Source
1. Clone the repository
2. Ensure you have .NET 10.0 SDK installed
3. Run the application:
   ```bash
   dotnet run
   ```

### Building for Distribution
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### Building the Installer

An Inno Setup installer is provided for easy distribution. To build it:

1. **Install Inno Setup** (free): Download from [jrsoftware.org](https://jrsoftware.org/isinfo.php)
2. **Run the build script**:
   ```powershell
   .\build-installer.ps1
   ```

The installer will be created as `installer\MicrophoneVolumeEnforcer-Setup.exe` and includes:
- ✅ **Desktop shortcut option** (checkbox during install)
- ✅ **Launch at Windows startup** (checkbox during install) 
- ✅ **Windows 10/11 compatible** (minimum Windows 10 build 17763)
- ✅ **Lightweight** (~10-15 MB with LZMA compression)
- ✅ **No admin privileges required** (user-level install)
- ✅ **Modern UI** with proper uninstall support
- ✅ **Automatic prerequisite checking** for .NET 10.0 and WebView2
- ✅ **Guided dependency installation** with direct download links
- ✅ **Smart detection** of missing system components

## Usage

### First Time Setup
1. Launch the application
2. Select your microphone from the dropdown list
3. Set your desired volume level (default: 100%)
4. Enable "Enforce Volume" to start monitoring
5. Configure your preferred settings in the Settings panel

### Daily Operation
- The application runs in the background and maintains your microphone volume
- Access via system tray icon when minimized
- Double-click tray icon to restore the window
- Use settings to customize behavior according to your preferences

### Keyboard Shortcuts
- **Alt+T**: Toggle between light and dark themes

## Technical Details

### Architecture
- **Frontend**: HTML/CSS/JavaScript with WebView2
- **Backend**: C# WPF application with CoreAudio integration
- **Audio Engine**: CoreAudio library for low-level Windows audio control
- **Storage**: JSON-based settings stored in user's AppData folder

### File Structure
```
MicrophoneVolumeEnforcer/
├── wwwroot/           # Web UI files
│   ├── index.html     # Main UI layout
│   ├── main.js        # Application logic
│   └── style.css      # Styling and themes
├── MainWindow.xaml    # WPF window definition
├── MainWindow.xaml.cs # Application logic and WebView2 integration
├── App.xaml           # WPF application configuration
└── App.xaml.cs        # Application startup
```

### Settings Storage
Settings are stored in: `%APPDATA%/MicrophoneVolumeEnforcer/settings.json`

## Troubleshooting

### Common Issues
- **No devices showing**: Ensure your microphone is connected and recognized by Windows
- **Volume not enforcing**: Check that the device is still active and selected
- **Application won't start with Windows**: Re-enable the setting to refresh registry entries

### Debug Information
The application includes comprehensive debug logging. To view debug output:
1. Run from Visual Studio in Debug mode, or
2. Check the Output window in Visual Studio (Debug output)

## Contributing

This application follows standard C# and web development practices. Key areas for contribution:
- UI/UX improvements
- Additional audio device features
- Cross-platform support
- Performance optimizations

## License

See [LICENSE.md](LICENSE.md)

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for detailed version history and changes.

## Overview

**Microphone Volume Enforcer** is a lightweight WPF application that automatically maintains your microphone volume at your desired level, preventing it from being accidentally changed by applications, drivers, or system updates.

## System Requirements

### Minimum Requirements
- **Operating System**: Windows 10 (Build 17763) or Windows 11
- **Architecture**: 64-bit (x64) systems only
- **.NET Runtime**: .NET 10.0 Desktop Runtime
- **WebView2**: Microsoft Edge WebView2 Runtime
- **Disk Space**: ~50 MB free space
- **Memory**: 100 MB RAM

### Automatic Dependency Checking
The installer automatically checks for required components and will:
- ✅ **Detect missing .NET 10.0 Runtime** and provide download links
- ✅ **Detect missing WebView2 Runtime** and provide download links  
- ✅ **Guide you through the installation process** with helpful dialogs
- ✅ **Allow you to continue anyway** if you prefer to install dependencies later

> **Note**: Most Windows 10/11 systems already have WebView2 pre-installed. .NET 10.0 Runtime may need to be downloaded for first-time installations.

## Key Features
