using System.ComponentModel; // Required for CancelEventArgs
using System.IO;
using System.Linq;
using System.Runtime.InteropServices; // Required for ClassInterface and ComVisible
using System.Windows;
using System.Windows.Controls; // For StackPanel, TextBlock, RadioButton, CheckBox, Button
using Microsoft.Web.WebView2.Core;
using CoreAudio; // Corrected using statement
using System.Threading.Tasks;
using System.Windows.Forms; // Required for NotifyIcon, ContextMenuStrip, etc.
using System.Drawing; // Required for Icon
using Microsoft.Win32; // Required for Registry
using System.Reflection; // Required for Assembly.GetExecutingAssembly
using System.Text.Json; // For JSON deserialization
using System;
using System.Threading; // Added for SynchronizationContext, separate from System.Windows.Forms.Timer
using System.Text.Json.Serialization;

namespace MicrophoneVolumeEnforcer;

// Helper class for deserializing settings
public class AppSettings
{
    public string? SelectedDevice { get; set; }
    public int TargetVolume { get; set; } = 100;
    public bool IsEnforced { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool EnforceAllDevices { get; set; } = true;
    public string CloseBehavior { get; set; } = "minimize"; // "minimize" | "ask" | "exit"
    public bool DontAskAgain { get; set; } = false;
    public string? RememberedCloseAction { get; set; } // "minimize" | "exit"
}

// DTO returned to JS so the dropdown can display a friendly name while keeping a stable MMDevice ID as the value.
public sealed record MicrophoneDeviceInfo(string Id, string Name);

// System.Text.Json source generation. Avoids reflection at runtime; small startup-time win.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(MicrophoneDeviceInfo))]
[JsonSerializable(typeof(MicrophoneDeviceInfo[]))]
internal partial class AppJsonContext : JsonSerializerContext;

// Single source of truth for settings.json I/O. Loaded synchronously at startup so MainWindow can honor StartMinimized before the window is shown.
public static class AppSettingsStore
{
    private const string AppFolder = "MicrophoneVolumeEnforcer";
    private const int MaxPayloadBytes = 32 * 1024;

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolder,
            "settings.json");

    public static AppSettings LoadOrDefault()
    {
        try
        {
            string path = SettingsPath;
            if (!File.Exists(path)) return new AppSettings();

            var info = new FileInfo(path);
            if (info.Length > MaxPayloadBytes) return new AppSettings();

            string json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings) ?? new AppSettings();
            MigrateSelectedDeviceIfNeeded(settings);
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppSettingsStore.LoadOrDefault failed: {ex.Message}");
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        SaveRaw(JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings));
    }

    // Pre-2.2.0 settings stored DeviceFriendlyName under selectedDevice. CoreAudio device IDs always start
    // with '{', so anything that doesn't is treated as a legacy friendly name; we resolve once and rewrite.
    // If the mic is unplugged at startup, the rewrite is skipped and we'll retry on the next launch.
    private static void MigrateSelectedDeviceIfNeeded(AppSettings settings)
    {
        if (string.IsNullOrEmpty(settings.SelectedDevice)) return;
        if (settings.SelectedDevice.StartsWith('{')) return;

        try
        {
            var enumerator = new MMDeviceEnumerator();
            var match = enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .FirstOrDefault(d => d.DeviceFriendlyName == settings.SelectedDevice);
            if (match != null)
            {
                settings.SelectedDevice = match.ID;
                SaveRaw(JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MigrateSelectedDeviceIfNeeded failed: {ex.Message}");
        }
    }

    public static string? LoadRaw()
    {
        try
        {
            string path = SettingsPath;
            if (!File.Exists(path)) return null;
            var info = new FileInfo(path);
            if (info.Length > MaxPayloadBytes) return null;
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppSettingsStore.LoadRaw failed: {ex.Message}");
            return null;
        }
    }

    public static void SaveRaw(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > MaxPayloadBytes)
            throw new InvalidOperationException("Settings payload too large.");

        string path = SettingsPath;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, json);
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private NotifyIcon? _notifyIcon;
    private bool _isExplicitlyClosing = false; // To track if exit is from tray menu
    private AppSettings _currentAppSettings = new AppSettings(); // Hold current settings
    private bool _balloonShownThisSession = false; // Track if balloon notification has been shown this session
    private HostBridge? _hostBridge;

    public MainWindow()
    {
        InitializeComponent();
        InitializeAsync();
        SetupTrayIcon();

        var initialSettings = AppSettingsStore.LoadOrDefault();
        if (initialSettings.StartMinimized)
        {
            // Skip the tray-balloon on startup: the user explicitly asked to start hidden, so the
            // "I'm in the tray now" balloon would be redundant noise.
            _balloonShownThisSession = true;
            WindowState = WindowState.Minimized;
        }
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new NotifyIcon();
        // Ensure app.ico is in the output directory and its Build Action is Content, Copy to Output Directory is Copy if newer.
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (File.Exists(iconPath))
        {
            _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
        }
        else
        {
            // Fallback or error handling if icon not found
            System.Windows.MessageBox.Show("Application icon 'app.ico' not found. This is a bug. I hate bugs.", "Icon Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _notifyIcon.Text = "Microphone Volume Enforcer";
        _notifyIcon.Visible = false; // Initially hidden, shown when minimized

        _notifyIcon.DoubleClick += (sender, args) => RestoreWindow();
        
        // Add balloon tip click event to restore window
        _notifyIcon.BalloonTipClicked += (sender, args) => RestoreWindow();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Restore", null, (s, e) => RestoreWindow());
        contextMenu.Items.Add("Minimize", null, (s, e) => { if (WindowState != WindowState.Minimized) WindowState = WindowState.Minimized; });
        contextMenu.Items.Add("-"); // Separator
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_notifyIcon != null) _notifyIcon.Visible = false;
        ShowInTaskbar = true;
    }

    private void ExitApplication()
    {
        _isExplicitlyClosing = true;
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized && !_isExplicitlyClosing) // Also check not explicitly closing
        {
            Hide();
            if (_notifyIcon != null) 
            {
                _notifyIcon.Visible = true;
                
                // Only show balloon notification once per session
                if (!_balloonShownThisSession)
                {
                    // Force the tray icon to be visible before showing balloon
                    System.Threading.Thread.Sleep(100);
                    
                    // Show balloon notification to inform user the app was minimized to tray
                    try
                    {
                        _notifyIcon.ShowBalloonTip(5000, "Microphone Volume Enforcer", 
                            "Minimized to tray. Double-click the icon to restore. Right click the tray icon for more options.", 
                            ToolTipIcon.Info);
                        
                        _balloonShownThisSession = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Balloon tip failed: {ex.Message}");
                    }
                }
            }
            ShowInTaskbar = false;
        }
        base.OnStateChanged(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitlyClosing)
        {
            _currentAppSettings = AppSettingsStore.LoadOrDefault();

            string actionToTake = _currentAppSettings.CloseBehavior;

            if (_currentAppSettings.CloseBehavior == "ask" && _currentAppSettings.DontAskAgain && !string.IsNullOrEmpty(_currentAppSettings.RememberedCloseAction))
            {
                actionToTake = _currentAppSettings.RememberedCloseAction;
            }
            else if (_currentAppSettings.CloseBehavior == "ask" && !_currentAppSettings.DontAskAgain)
            {
                var dialog = new CloseConfirmationDialog { Owner = this };
                var result = dialog.ShowDialog();

                if (result == true)
                {
                    actionToTake = dialog.SelectedAction;

                    if (dialog.RememberChoice)
                    {
                        _currentAppSettings.DontAskAgain = true;
                        _currentAppSettings.RememberedCloseAction = dialog.SelectedAction;
                        try { AppSettingsStore.Save(_currentAppSettings); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OnClosing save failed: {ex.Message}"); }
                    }
                }
                else
                {
                    e.Cancel = true;
                    base.OnClosing(e);
                    return;
                }
            }

            // Execute the chosen action
            if (actionToTake == "minimize")
            {
                e.Cancel = true; 
                WindowState = WindowState.Minimized; 
            }
            else if (actionToTake == "exit")
            {
                ExitApplication(); 
            }
            else 
            {
                // Fallback to minimize if action is unclear
                e.Cancel = true; 
                WindowState = WindowState.Minimized; 
            }
        }
        base.OnClosing(e);
    }

    async void InitializeAsync()
    {
        // %LOCALAPPDATA% survives disk-cleanup / temp wipers, so theme + WebView2 cookies stay across reboots.
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MicrophoneVolumeEnforcer", "WebView2");
        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await webView.EnsureCoreWebView2Async(env);

        // SECURITY HARDENING: a tray-resident WPF app does not need browser features. Disable
        // everything that isn't the host bridge + our packaged page.
        var settings = webView.CoreWebView2.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false; // F12 / Ctrl+P / Ctrl+S / Ctrl+R
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;

        // Drag-drop, popups, and downloads should not happen from the embedded UI.
        webView.AllowExternalDrop = false;
        webView.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
        webView.CoreWebView2.DownloadStarting += (_, args) => args.Cancel = true;

        // Attach native host object. Field-stored so OnClosed can dispose the underlying CoreAudio state.
        _hostBridge = new HostBridge(this);
        webView.CoreWebView2.AddHostObjectToScript("nativeHost", _hostBridge);
        webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;

        // Determine path to packaged index.html
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string indexPath = Path.Combine(appDirectory, "wwwroot", "index.html");

        // Allow only the packaged index.html (and about:blank for internal navigation)
        string allowedUri = new Uri(indexPath).AbsoluteUri;
        webView.CoreWebView2.NavigationStarting += (_, navArgs) =>
        {
            if (!navArgs.Uri.Equals(allowedUri, StringComparison.OrdinalIgnoreCase) &&
                !navArgs.Uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                navArgs.Cancel = true; // Block untrusted navigation
            }
        };

        if (File.Exists(indexPath))
        {
            webView.Source = new Uri(indexPath);
        }
        else
        {
            webView.CoreWebView2.NavigateToString("<html><body><h1>Error: index.html not found</h1><p>Please check application deployment.</p></body></html>");
            System.Windows.MessageBox.Show($"Error: index.html not found at {indexPath}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        // WebView2 first: kills the JS engine so any in-flight nativeHost.* call cannot land on a disposed
        // HostBridge across the COM boundary. Then HostBridge tears down CoreAudio. Tray icon last.
        webView?.Dispose();
        _hostBridge?.Dispose();
        _hostBridge = null;
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        base.OnClosed(e);
    }
}

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class HostBridge : IDisposable
{
    private MainWindow _mainWindow;
    private readonly MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();
    private readonly SynchronizationContext? _syncContext;
    private const string AppNameForStartup = "MicrophoneVolumeEnforcer"; // Name for registry key

    // Per-device enforcement context. One instance lives in _enforced per actively-enforced capture endpoint.
    private sealed class EnforcedDevice : IDisposable
    {
        public required MMDevice Device { get; init; }
        public required AudioEndpointVolume Volume { get; init; }
        public required AudioEndpointVolumeNotificationDelegate Handler { get; init; }
        public required System.Threading.Timer Timer { get; init; }
        public bool PendingChange;

        public void Dispose()
        {
            // COM objects may already be torn down by Windows when we hit this during app shutdown
            // or device unplug; swallow but log so a real crash here isn't silent.
            try { Volume.OnVolumeNotification -= Handler; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"EnforcedDevice.Dispose: detach handler failed for {Device.ID}: {ex.Message}"); }
            try { Timer.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"EnforcedDevice.Dispose: timer dispose failed for {Device.ID}: {ex.Message}"); }
            try { Device.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"EnforcedDevice.Dispose: device dispose failed: {ex.Message}"); }
        }
    }

    private readonly Dictionary<string, EnforcedDevice> _enforced = new(); // key = MMDevice.ID
    private bool _isEnforcementEnabled = false;
    private bool _enforceAll = true;
    private float _targetVolume = 1.0f;
    private System.Threading.Timer? _deviceReconcileTimer;
    private readonly TimeSpan _enforcementGracePeriod = TimeSpan.FromSeconds(1);
    // CoreAudio 1.40.0 keeps RegisterEndpointNotificationCallback internal so we cannot subscribe to device add/remove events.
    // Poll the enumerator every 2 seconds while EnforceAll is on; CPU cost negligible, hot-plug latency <= 2s.
    private readonly TimeSpan _reconcileInterval = TimeSpan.FromSeconds(2);

    public HostBridge(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _syncContext = SynchronizationContext.Current;
    }

    public string GetHostVersion()
    {
        return "MicrophoneVolumeEnforcer Host v1.0 with CoreAudio";
    }

    public string GetMicrophoneDevices()
    {
        // Returned shape: JSON-encoded MicrophoneDeviceInfo[]. JS parses and uses Id as the dropdown value.
        // Single-string return avoids COM array marshalling and lets us include both id and friendly name.
        var devices = _enumerator
            .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => new MicrophoneDeviceInfo(d.ID, d.DeviceFriendlyName))
            .ToArray();
        return JsonSerializer.Serialize(devices, AppJsonContext.Default.MicrophoneDeviceInfoArray);
    }

    public void SetMicrophoneVolume(string deviceId, int volume)
    {
        if (!IsValidDeviceId(deviceId))
            throw new InvalidOperationException("Invalid device identifier.");

        var device = TryGetDeviceById(deviceId)
            ?? throw new InvalidOperationException($"Device not found: {deviceId}");

        if (device.AudioEndpointVolume == null)
            throw new InvalidOperationException($"AudioEndpointVolume unavailable for device: {deviceId}");

        float scalar = volume / 100.0f;
        if (scalar < 0.0f) scalar = 0.0f;
        if (scalar > 1.0f) scalar = 1.0f;
        device.AudioEndpointVolume.MasterVolumeLevelScalar = scalar;
    }

    private MMDevice? TryGetDeviceById(string id)
    {
        try { return _enumerator.GetDevice(id); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetDevice failed for {id}: {ex.Message}");
            return null;
        }
    }

    public void SaveSettings(string settingsJson)
    {
        // Lets JS surface the error via setStatus. UI-thread MessageBox from a tray-resident app is hostile.
        AppSettingsStore.SaveRaw(settingsJson);
    }

    public string? LoadSettings()
    {
        return AppSettingsStore.LoadRaw();
    }

    public void SetEnforcement(string deviceId, int volume, bool enable, bool enforceAll)
    {
        _isEnforcementEnabled = enable;
        _enforceAll = enforceAll;

        float clamped = volume / 100.0f;
        if (clamped < 0.0f) clamped = 0.0f;
        if (clamped > 1.0f) clamped = 1.0f;
        _targetVolume = clamped;

        if (!enable)
        {
            StopReconcileTimer();
            DetachAll();
            return;
        }

        if (enforceAll)
        {
            AttachAllActiveDevices();
            StartReconcileTimer();
        }
        else
        {
            StopReconcileTimer();
            AttachOnlySelectedDevice(deviceId);
        }

        RetargetAllToCurrentVolume();
    }

    private void AttachAllActiveDevices()
    {
        MMDevice[] devices;
        try
        {
            devices = _enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .ToArray();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AttachAllActiveDevices: enumerate failed: {ex.Message}");
            return;
        }

        foreach (var dev in devices)
        {
            if (_enforced.ContainsKey(dev.ID))
            {
                dev.Dispose();
                continue;
            }
            AttachEnforcement(dev);
        }
    }

    private void AttachOnlySelectedDevice(string deviceId)
    {
        if (!IsValidDeviceId(deviceId))
        {
            DetachAll();
            return;
        }

        MMDevice? target = TryGetDeviceById(deviceId);
        string? targetId = target?.ID;

        foreach (var id in _enforced.Keys.Where(k => k != targetId).ToList())
            DetachEnforcement(id);

        if (target == null) return;

        if (_enforced.ContainsKey(target.ID))
            target.Dispose();
        else
            AttachEnforcement(target);
    }

    private void AttachEnforcement(MMDevice device)
    {
        if (_enforced.ContainsKey(device.ID))
        {
            device.Dispose();
            return;
        }

        var endpointVolume = device.AudioEndpointVolume;
        if (endpointVolume == null)
        {
            device.Dispose();
            return;
        }

        string deviceId = device.ID;
        var handler = new AudioEndpointVolumeNotificationDelegate(data => OnDeviceVolumeChanged(deviceId, data));
        var timer = new System.Threading.Timer(
            _ => ForceEnforceVolume(deviceId), null,
            System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);

        try { endpointVolume.MasterVolumeLevelScalar = _targetVolume; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AttachEnforcement: initial set failed for {deviceId}: {ex.Message}");
        }
        endpointVolume.OnVolumeNotification += handler;

        _enforced[deviceId] = new EnforcedDevice
        {
            Device = device,
            Volume = endpointVolume,
            Handler = handler,
            Timer = timer,
        };
    }

    private void DetachEnforcement(string deviceId)
    {
        if (_enforced.Remove(deviceId, out var ctx)) ctx.Dispose();
    }

    private void DetachAll()
    {
        foreach (var ctx in _enforced.Values) ctx.Dispose();
        _enforced.Clear();
    }

    private void RetargetAllToCurrentVolume()
    {
        foreach (var ctx in _enforced.Values)
        {
            try { ctx.Volume.MasterVolumeLevelScalar = _targetVolume; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RetargetAllToCurrentVolume failed for {ctx.Device.ID}: {ex.Message}");
            }
        }
    }

    private void StartReconcileTimer()
    {
        if (_deviceReconcileTimer != null) return;
        _deviceReconcileTimer = new System.Threading.Timer(
            ReconcileEnforceAll, null,
            _reconcileInterval, _reconcileInterval);
    }

    private void StopReconcileTimer()
    {
        _deviceReconcileTimer?.Dispose();
        _deviceReconcileTimer = null;
    }

    private void ReconcileEnforceAll(object? state)
    {
        _syncContext?.Post(_ =>
        {
            if (!_isEnforcementEnabled || !_enforceAll) return;
                try
            {
                var current = _enumerator
                    .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .ToDictionary(d => d.ID);

                foreach (var pair in current)
                {
                    if (!_enforced.ContainsKey(pair.Key))
                        AttachEnforcement(pair.Value);
                    else
                        pair.Value.Dispose();
                }

                foreach (var goneId in _enforced.Keys.Where(k => !current.ContainsKey(k)).ToList())
                    DetachEnforcement(goneId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReconcileEnforceAll failed: {ex.Message}");
            }
        }, null);
    }

    private void OnDeviceVolumeChanged(string deviceId, AudioVolumeNotificationData data)
    {
        // COM-thread early-out: skip if we don't need to do anything. _isEnforcementEnabled is a bool and
        // _targetVolume is a float; reads from another thread are atomic on x64, so this race is benign.
        if (!_isEnforcementEnabled) return;
        if (Math.Abs(data.MasterVolume - _targetVolume) <= 0.01f) return;

        _syncContext?.Post(_ =>
        {
            if (!_isEnforcementEnabled) return;
            if (!_enforced.TryGetValue(deviceId, out var ctx)) return;
            ctx.PendingChange = true;
            ctx.Timer.Change(_enforcementGracePeriod, System.Threading.Timeout.InfiniteTimeSpan);
        }, null);
    }

    private void ForceEnforceVolume(string deviceId)
    {
        _syncContext?.Post(_ =>
        {
            if (!_isEnforcementEnabled) return;
            if (!_enforced.TryGetValue(deviceId, out var ctx)) return;
            if (!ctx.PendingChange) return;
            try
            {
                ctx.Volume.MasterVolumeLevelScalar = _targetVolume;
                ctx.PendingChange = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForceEnforceVolume failed for {deviceId}: {ex.Message}");
            }
        }, null);
    }

    public void SetStartWithWindows(bool enable)
    {
        string? executablePath = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrEmpty(executablePath))
            throw new InvalidOperationException("Could not determine application executable path.");

        using RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        if (rk == null)
            throw new InvalidOperationException("Could not open registry key for startup.");

        if (enable)
            rk.SetValue(AppNameForStartup, executablePath);
        else
            rk.DeleteValue(AppNameForStartup, false);
    }

    public bool GetStartWithWindows()
    {
        // Best-effort read; on failure the JS layer falls back to the settings.json value, so swallow + log.
        try
        {
            using RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
            if (rk == null) return false;
            return !string.IsNullOrEmpty((string?)rk.GetValue(AppNameForStartup));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetStartWithWindows failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _isEnforcementEnabled = false;
        StopReconcileTimer();
        DetachAll();
        // MMDeviceEnumerator is not IDisposable in CoreAudio 1.40.0; GC handles the underlying COM RCW.
    }

    // --------------------------- SECURITY HELPERS ---------------------------
    // CoreAudio device IDs look like {0.0.1.00000000}.{guid}: braces, dots, hex chars, hyphens.
    // Length cap is generous; the format is fixed but we don't pin it to be tolerant of future changes.
    private static bool IsValidDeviceId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id!.Length > 256) return false;
        return id.All(ch => !char.IsControl(ch) && ch != '\\' && ch != '/');
    }
}

public class CloseConfirmationDialog : Window
{
    public string SelectedAction { get; private set; } = "minimize";
    public bool RememberChoice { get; private set; } = false;

    public CloseConfirmationDialog()
    {
        Title = "You're not getting away that easily!";
        Width = 480;
        Height = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var mainPanel = new System.Windows.Controls.DockPanel
        {
            Margin = new Thickness(25, 20, 25, 20)
        };

        // Button panel at bottom
        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 80,
            Height = 30,
            Margin = new Thickness(0, 0, 15, 0),
            IsDefault = true,
            FontSize = 10
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 80,
            Height = 30,
            IsCancel = true,
            FontSize = 10
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        // Dock buttons to bottom
        System.Windows.Controls.DockPanel.SetDock(buttonPanel, System.Windows.Controls.Dock.Bottom);
        mainPanel.Children.Add(buttonPanel);

        // Content panel
        var contentPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 15)
        };

        // Message text
        var messageText = new System.Windows.Controls.TextBlock
        {
            Text = "Now.. Do you want to exit or keep running in the background?\n" +
                   "I recommend the latter.",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 25),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        contentPanel.Children.Add(messageText);

        // Radio buttons for actions
        var minimizeRadio = new System.Windows.Controls.RadioButton
        {
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "Minimize to Tray - Keep running in the background",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            },
            IsChecked = true,
            Margin = new Thickness(0, 8, 0, 8),
            GroupName = "CloseAction",
            Padding = new Thickness(8, 4, 0, 4)
        };
        contentPanel.Children.Add(minimizeRadio);

        var exitRadio = new System.Windows.Controls.RadioButton
        {
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "Exit Application\nI will be devestated and will cry myself to sleep",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            },
            Margin = new Thickness(0, 8, 0, 25),
            GroupName = "CloseAction",
            Padding = new Thickness(8, 4, 0, 4)
        };
        contentPanel.Children.Add(exitRadio);

        // Remember choice checkbox
        var rememberCheckBox = new System.Windows.Controls.CheckBox
        {
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "Remember my choice and stop nagging me\n(I didn't check if this works, but it should, technically..)",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            },
            Margin = new Thickness(0, 15, 0, 20),
            Padding = new Thickness(8, 4, 0, 4)
        };
        contentPanel.Children.Add(rememberCheckBox);

        // Add content panel to main panel
        mainPanel.Children.Add(contentPanel);

        // Event handlers
        okButton.Click += (s, e) =>
        {
            SelectedAction = minimizeRadio.IsChecked == true ? "minimize" : "exit";
            RememberChoice = rememberCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        };

        cancelButton.Click += (s, e) =>
        {
            DialogResult = false;
            Close();
        };

        Content = mainPanel;
    }
} 