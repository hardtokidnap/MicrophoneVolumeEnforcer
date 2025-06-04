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
    public string CloseBehavior { get; set; } = "minimize"; // Default to minimize: "minimize", "ask", "exit"
    public bool DontAskAgain { get; set; } = false;
    public string? RememberedCloseAction { get; set; } // "minimize" or "exit"
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

    public MainWindow()
    {
        InitializeComponent();
        InitializeAsync();
        SetupTrayIcon();
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
                            "HEY YOU, i'm in the tray now and I'm still kicking. If you want to close me, you can right click → Exit or disable me in the settings.", 
                            ToolTipIcon.Info);
                        
                        _balloonShownThisSession = true; // Mark as shown for this session
                        
                        // Debug output to verify the method is called
                      //  System.Diagnostics.Debug.WriteLine("Balloon tip shown at: " + DateTime.Now);
                     }
                     catch (Exception)
                     {
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
            // Load current settings
            string? settingsJson = new HostBridge(this).LoadSettingsInternal(); 
            System.Diagnostics.Debug.WriteLine($"Raw settings JSON: {settingsJson ?? "NULL"}");
            
            if (!string.IsNullOrEmpty(settingsJson))
            {
                try { 
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    _currentAppSettings = JsonSerializer.Deserialize<AppSettings>(settingsJson, options) ?? new AppSettings(); 
                    System.Diagnostics.Debug.WriteLine($"Loaded settings: CloseBehavior='{_currentAppSettings.CloseBehavior}', DontAskAgain={_currentAppSettings.DontAskAgain}");
                }
                catch (Exception ex) { 
                    System.Diagnostics.Debug.WriteLine($"Error deserializing settings: {ex.Message}");
                    _currentAppSettings = new AppSettings(); 
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No settings found, using defaults");
                _currentAppSettings = new AppSettings(); // Ensure defaults if no settings file
            }

            string actionToTake = _currentAppSettings.CloseBehavior;
            System.Diagnostics.Debug.WriteLine($"Initial actionToTake: '{actionToTake}'");

            // Check if user previously chose "don't ask again" 
            if (_currentAppSettings.CloseBehavior == "ask" && _currentAppSettings.DontAskAgain && !string.IsNullOrEmpty(_currentAppSettings.RememberedCloseAction))
            {
                actionToTake = _currentAppSettings.RememberedCloseAction;
                System.Diagnostics.Debug.WriteLine($"Using remembered action: '{actionToTake}'");
            }
            // Show dialog if close behavior is "ask" and user hasn't chosen "don't ask again"
            else if (_currentAppSettings.CloseBehavior == "ask" && !_currentAppSettings.DontAskAgain)
            {
                System.Diagnostics.Debug.WriteLine("Showing close confirmation dialog");
                // Create a custom dialog
                var dialog = new CloseConfirmationDialog();
                dialog.Owner = this; // Set owner for proper modal behavior
                var result = dialog.ShowDialog();

                if (result == true)
                {
                    actionToTake = dialog.SelectedAction;
                    System.Diagnostics.Debug.WriteLine($"User selected action: '{actionToTake}'");
                    
                    // If user chose to remember their choice, save it
                    if (dialog.RememberChoice)
                    {
                        _currentAppSettings.DontAskAgain = true;
                        _currentAppSettings.RememberedCloseAction = dialog.SelectedAction;
                        // Save the updated settings
                        try
                        {
                            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                            string updatedSettingsJson = JsonSerializer.Serialize(_currentAppSettings, options);
                            new HostBridge(this).SaveSettings(updatedSettingsJson);
                            System.Diagnostics.Debug.WriteLine("Saved updated settings with remembered choice");
                        }
                        catch (Exception ex) { 
                            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("User cancelled dialog");
                    e.Cancel = true; 
                    base.OnClosing(e);
                    return;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Final actionToTake: '{actionToTake}'");

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
        var userDataFolder = Path.Combine(Path.GetTempPath(), "MicrophoneVolumeEnforcer_WebView2");
        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await webView.EnsureCoreWebView2Async(env);
        webView.CoreWebView2.AddHostObjectToScript("nativeHost", new HostBridge(this));
        webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string indexPath = Path.Combine(appDirectory, "wwwroot", "index.html");

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
}

[ClassInterface(ClassInterfaceType.AutoDual)] 
[ComVisible(true)] 
public class HostBridge
{
    private MainWindow _mainWindow;
    private static MMDeviceEnumerator? _staticDeviceEnumerator; 
    private readonly SynchronizationContext? _syncContext; 
    private const string AppNameForStartup = "MicrophoneVolumeEnforcer"; // Name for registry key

    private MMDevice? _enforcedDevice = null;
    private AudioEndpointVolume? _audioEndpointVolume = null;
    private bool _isEnforcementEnabled = false;
    private float _targetVolume = 1.0f; 
    private AudioEndpointVolumeNotificationDelegate? _volumeNotificationHandler = null;
    private System.Threading.Timer? _enforcementTimer; // Fully qualify Timer
    private bool _volumeHasBeenChangedDuringCooldown = false;
    private readonly TimeSpan _enforcementGracePeriod = TimeSpan.FromSeconds(1); 

    public HostBridge(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _syncContext = SynchronizationContext.Current; 
        _staticDeviceEnumerator ??= new MMDeviceEnumerator(); 
    }

    public string GetHostVersion()
    {
        return "MicrophoneVolumeEnforcer Host v1.0 with CoreAudio";
    }

    public string[] GetMicrophoneDevices()
    {
        try
        {
            _staticDeviceEnumerator ??= new MMDeviceEnumerator(); 
            var enumerator = _staticDeviceEnumerator;
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                                    .ToArray(); 
            
            if (!devices.Any()) {
                System.Windows.MessageBox.Show("[C# HostBridge] No active capture devices found.", "CoreAudio Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return Array.Empty<string>();
            }
            string[] deviceNames = devices.Select(d => d.DeviceFriendlyName).ToArray();
            return deviceNames;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"[C# HostBridge] Error getting devices: {ex.Message}", "CoreAudio Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return new string[] { $"Error: {ex.Message}" };
        }
    }

    public void SetMicrophoneVolume(string deviceId, int volume)
    {
        try
        {
            _staticDeviceEnumerator ??= new MMDeviceEnumerator();
            var enumerator = _staticDeviceEnumerator;
            var device = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                                 .FirstOrDefault(d => d.DeviceFriendlyName == deviceId);

            if (device != null)
            {
                float volumeScalar = volume / 100.0f;
                if (volumeScalar < 0.0f) volumeScalar = 0.0f;
                if (volumeScalar > 1.0f) volumeScalar = 1.0f;

                if (device.AudioEndpointVolume != null)
                {
                    device.AudioEndpointVolume.MasterVolumeLevelScalar = volumeScalar;
                    if (_isEnforcementEnabled && _enforcedDevice != null && _enforcedDevice.ID == device.ID)
                    {
                        _targetVolume = volumeScalar;
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show($"[C# HostBridge] AudioEndpointVolume is null for device: {deviceId}", "CoreAudio Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                System.Windows.MessageBox.Show($"[C# HostBridge] Device not found: {deviceId}", "CoreAudio Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"[C# HostBridge] Error setting volume for {deviceId}: {ex.Message}", "CoreAudio Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void SaveSettings(string settingsJson)
    {
        try
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDir = Path.Combine(appDataPath, AppNameForStartup); // Use constant
            Directory.CreateDirectory(settingsDir); 
            string settingsFile = Path.Combine(settingsDir, "settings.json");
            File.WriteAllText(settingsFile, settingsJson);
        }
        catch (Exception ex)
        {
             System.Windows.MessageBox.Show($"[C# HostBridge] Error saving settings: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public string? LoadSettings()
    {
        try
        {
            return LoadSettingsInternal(); // Use the internal method
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"[C# HostBridge] Error loading settings: {ex.Message}", "File Error", MessageBoxButton.OK, MessageBoxImage.Error); 
            return null;
        }
    }

    public void SetEnforcement(string deviceId, int volume, bool enable)
    {
        if (_audioEndpointVolume != null && _volumeNotificationHandler != null)
        {
            _audioEndpointVolume.OnVolumeNotification -= _volumeNotificationHandler;
        }
        _isEnforcementEnabled = enable;
        _targetVolume = volume / 100.0f;
        if (_targetVolume < 0.0f) _targetVolume = 0.0f;
        if (_targetVolume > 1.0f) _targetVolume = 1.0f;
        _enforcedDevice = null;
        _audioEndpointVolume = null;
        _volumeNotificationHandler = null;
        _volumeHasBeenChangedDuringCooldown = false;

        _enforcementTimer?.Dispose();
        _enforcementTimer = null;

        if (enable)
        {
            _staticDeviceEnumerator ??= new MMDeviceEnumerator();
            var enumerator = _staticDeviceEnumerator;
            var device = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                                   .FirstOrDefault(d => d.DeviceFriendlyName == deviceId);
            if (device != null && device.AudioEndpointVolume != null)
            {
                _enforcedDevice = device;
                _audioEndpointVolume = device.AudioEndpointVolume;
                _audioEndpointVolume.MasterVolumeLevelScalar = _targetVolume;
                _volumeNotificationHandler = new AudioEndpointVolumeNotificationDelegate(OnVolumeNotification);
                _audioEndpointVolume.OnVolumeNotification += _volumeNotificationHandler;
                _enforcementTimer = new System.Threading.Timer(ForceEnforceVolumeAfterCooldown, null, System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        if (_isEnforcementEnabled && 
            _audioEndpointVolume != null && 
            Math.Abs(data.MasterVolume - _targetVolume) > 0.01f)
        {
            _syncContext?.Post(_ => 
            {
                _volumeHasBeenChangedDuringCooldown = true;
                _enforcementTimer?.Change(_enforcementGracePeriod, System.Threading.Timeout.InfiniteTimeSpan);
                System.Diagnostics.Debug.WriteLine($"[{DateTime.UtcNow}] Volume change detected for {_enforcedDevice?.DeviceFriendlyName}. Grace period timer (re)started on UI thread.");
            }, null);
        }
    }

    private void ForceEnforceVolumeAfterCooldown(object? state)
    {
        _syncContext?.Post(_ =>
        {
            if (_isEnforcementEnabled && _volumeHasBeenChangedDuringCooldown && _audioEndpointVolume != null)
            {
                try
                {
                    _audioEndpointVolume.MasterVolumeLevelScalar = _targetVolume;
                    _volumeHasBeenChangedDuringCooldown = false; 
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.UtcNow}] Enforced volume for {_enforcedDevice?.DeviceFriendlyName} after grace period on UI thread.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.UtcNow}] Error enforcing volume after grace period on UI thread: {ex.Message}");
                }
            }
        }, null);
    }

    public void SetStartWithWindows(bool enable)
    {
        try
        {
            string? executablePath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(executablePath))
            {
                System.Windows.MessageBox.Show("Error: Could not determine application executable path.", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (rk == null)
            {
                System.Windows.MessageBox.Show("Error: Could not open registry key for startup.", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (enable)
            {
                rk.SetValue(AppNameForStartup, executablePath);
            }
            else
            {
                rk.DeleteValue(AppNameForStartup, false); // Do not throw if not found
            }
            rk.Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error setting startup state: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public bool GetStartWithWindows()
    {
        try
        {
            RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
            if (rk == null) return false;
            
            string? value = (string?)rk.GetValue(AppNameForStartup);
            rk.Close();
            return !string.IsNullOrEmpty(value);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error getting startup state: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    // Internal method for MainWindow to get raw JSON without JS involvement for OnClosing
    // This avoids needing JS to be fully loaded if OnClosing is called early.
    public string? LoadSettingsInternal() 
    {
        try
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDir = Path.Combine(appDataPath, AppNameForStartup);
            string settingsFile = Path.Combine(settingsDir, "settings.json");
            System.Diagnostics.Debug.WriteLine($"Settings file path: {settingsFile}");
            if (File.Exists(settingsFile))
            {
                string content = File.ReadAllText(settingsFile);
                System.Diagnostics.Debug.WriteLine($"Settings file content: {content}");
                return content;
            }
            System.Diagnostics.Debug.WriteLine("Settings file does not exist");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading settings file: {ex.Message}");
            return null; // Don't show MessageBox here, as it might be during shutdown
        }
    }
}

public class CloseConfirmationDialog : Window
{
    public string SelectedAction { get; private set; } = "minimize";
    public bool RememberChoice { get; private set; } = false;

    public CloseConfirmationDialog()
    {
        Title = "Close Behavior";
        Width = 480;
        Height = 380;
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
            FontSize = 14
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 80,
            Height = 30,
            IsCancel = true,
            FontSize = 14
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
            Text = "What would you like to do when closing the application?",
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
            Content = "Minimize to Tray - Keep running in the background",
            IsChecked = true,
            Margin = new Thickness(0, 8, 0, 8),
            GroupName = "CloseAction",
            FontSize = 14,
            Padding = new Thickness(8, 4, 0, 4)
        };
        contentPanel.Children.Add(minimizeRadio);

        var exitRadio = new System.Windows.Controls.RadioButton
        {
            Content = "Exit Application - Completely close the application",
            Margin = new Thickness(0, 8, 0, 25),
            GroupName = "CloseAction",
            FontSize = 14,
            Padding = new Thickness(8, 4, 0, 4)
        };
        contentPanel.Children.Add(exitRadio);

        // Remember choice checkbox
        var rememberCheckBox = new System.Windows.Controls.CheckBox
        {
            Content = "Remember my choice and don't ask again",
            Margin = new Thickness(0, 15, 0, 20),
            FontSize = 13,
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