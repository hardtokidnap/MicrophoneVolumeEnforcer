# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
- **Windows 10**: Minimum supported version
- **Windows 11**: Fully supported and tested
- **WebView2 Runtime**: Required (usually pre-installed) 