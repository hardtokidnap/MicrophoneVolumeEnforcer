# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Start minimized to tray**: new setting (off by default) for users who launch the app at Windows startup and don't want the window popping up every login.
- **Enforce on all microphones**: new setting (on by default) extends volume enforcement to every active capture endpoint, not just the selected one. Newly plugged microphones join enforcement within ~2 seconds; unplugged ones drop cleanly.

### Changed
- Target framework upgraded from .NET 8.0 to .NET 10.0 (LTS, supported until November 2028).
- WebView2 SDK upgraded from 1.0.2210.55 to 1.0.3967.48.
- Installer prerequisite check updated to detect .NET 10.0 Desktop Runtime; download link points at the .NET 10 download page.
- Microphone dropdown values are now stable device IDs instead of friendly names. Settings files from older versions are migrated automatically on first launch.
- WebView2 user data folder moved from `%TEMP%` to `%LOCALAPPDATA%`, so theme and other state survive disk-cleanup tools.
- System.Text.Json source generation enabled for settings serialization; one-time `JsonSerializerOptions` instance cached in `AppSettingsStore`.
- HostBridge owns a single MMDeviceEnumerator instance; dropped the stale per-call null-check pattern.

### Fixed
- Error popups removed from hot paths (settings save, volume change, device enumeration, startup-registry read/write). Errors now surface inline in the UI status area instead of blocking dialogs.
- HostBridge implements IDisposable; MainWindow disposes WebView2, HostBridge, then tray icon on close, so CoreAudio handlers and timers are torn down cleanly.

### Security
- WebView2 user data folder is now scoped under `%LOCALAPPDATA%` rather than the shared `%TEMP%` directory.
- Device identifier validation tightened to handle CoreAudio's `{guid}.{guid}` ID format.

## [Released 2.1.0] - 2025-06-04

### Added
- **Modern WPF Application**: Complete rewrite from console service to WPF application with WebView2
- **Web-Based User Interface**: Modern HTML/CSS/JavaScript interface with responsive design
- **Dark/Light Theme Support**: Toggle between themes with system preference detection
- **System Tray Integration**: Minimize to system tray with balloon notifications
- **Advanced Settings Panel**: Comprehensive configuration options
- **Close Behavior Configuration**: Choose between minimize, ask, or exit when closing
- **Auto-Save Functionality**: All settings automatically saved without manual intervention
- **Start with Windows**: Registry integration for Windows startup
- **Keyboard Shortcuts**: Alt+T for theme toggling
- **Enhanced Tray Menu**: Restore, Minimize, and Exit options
- **Session-Based Notifications**: Smart balloon notifications (only once per session)
- **Real-Time Volume Updates**: Live volume adjustment with visual feedback
- **Device Selection UI**: Dropdown menu for microphone selection
- **Volume Slider**: Interactive volume control with percentage display
- **Settings Persistence**: JSON-based configuration storage in AppData
- **Debug Logging**: Comprehensive debug output for troubleshooting
- **Window State Management**: Proper minimize/restore functionality
- **Custom Close Dialog**: User-friendly dialog for close behavior selection

### Changed
- **Architecture**: Migrated from console service to WPF + WebView2 architecture
- **Audio Engine**: Enhanced CoreAudio integration with better error handling
- **User Experience**: Complete UI/UX overhaul with modern design principles
- **Settings Storage**: Changed from service configuration to JSON files
- **Application Lifecycle**: Improved startup, shutdown, and background operation

### Removed
- **Console Service**: Removed Topshelf-based Windows service implementation
- **Command Line Interface**: Replaced with graphical user interface
- **Service Dependencies**: Removed Topshelf dependency

### Fixed
- **Volume Enforcement**: Improved reliability of volume monitoring and enforcement
- **Memory Management**: Better resource cleanup and disposal
- **Error Handling**: Enhanced error reporting and graceful failure handling
- **Settings Persistence**: Reliable saving and loading of user preferences

### Security
- **Navigation Hardening** – WebView2 now blocks navigation to external origins and disables DevTools, context menus and script dialogs.
- **Input Validation** – Host validates microphone device identifiers and limits settings payload size.
- **Settings Size Guard** – Prevents denial-of-service via oversized JSON in `SaveSettings`.


## [1.0.0] - 2024-01-28

### Added
- **Initial Release**: Basic console service implementation
- **CoreAudio Integration**: Basic microphone volume monitoring
- **Windows Service**: Topshelf-based service architecture
- **Volume Enforcement**: Automatic volume correction to 100%
- **Device Enumeration**: Detection of active capture devices

### Technical Notes

#### Breaking Changes in 2.1.0
- Complete rewrite requires fresh installation
- Settings from 1.x versions are not compatible
- Windows Service registration needs to be manually removed if upgrading

#### Migration Guide
1. Uninstall/stop the old service version
2. Install the new WPF application
3. Reconfigure your preferences in the new settings panel

#### Dependencies
- **.NET 8.0**: Updated from previous .NET version
- **WebView2**: New dependency for modern UI
- **CoreAudio 1.40.0**: Maintained for audio functionality
- **Windows Forms**: Added for system tray functionality

#### Platform Support
- **Windows 7/8/8.1**: Take you 'Ol reliable somewhere else.
- **Windows 10**: Minimum supported version
- **Windows 11**: Fully supported and tested
- **WebView2 Runtime**: Required (usually pre-installed) 