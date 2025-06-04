document.addEventListener('DOMContentLoaded', () => {
    const microphoneSelect = document.getElementById('microphone-select');
    const refreshButton = document.getElementById('refresh-devices-button');
    const volumeSlider = document.getElementById('volume-slider');
    const volumeDisplay = document.getElementById('volume-display');
    const enforceCheckbox = document.getElementById('enforce-volume-checkbox');
    const saveButton = document.getElementById('save-settings-button');
    const statusMessageContainer = document.querySelector('.container');
    const themeToggleButton = document.getElementById('theme-toggle-button');
    const sunSvgIconInstance = document.getElementById('theme-icon-sun-instance');
    const moonSvgIconInstance = document.getElementById('theme-icon-moon-instance');

    const mainView = document.getElementById('main-view');
    const settingsView = document.getElementById('settings-view');
    const settingsToggleButton = document.getElementById('settings-toggle-button');
    const backToMainButton = document.getElementById('back-to-main-button');

    const closeBehaviorSelect = document.getElementById('close-behavior-select');
    const askOnceGroup = document.getElementById('ask-once-group');
    const startWithWindowsCheckbox = document.getElementById('start-with-windows-checkbox');
    const dontAskAgainCheckbox = document.getElementById('dont-ask-again-checkbox');

    let nativeHost = null;
    let statusClearTimer = null;

    // Debounce function
    function debounce(func, delay) {
        let timeout;
        return function(...args) {
            const context = this;
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(context, args), delay);
        };
    }

    async function initializeApp() {
        setStatus('Initializing... attempting to connect to backend.', 'info');
        try {
            if (window.chrome && window.chrome.webview && window.chrome.webview.hostObjects) {
                nativeHost = await window.chrome.webview.hostObjects.nativeHost;
                if (nativeHost) {
                    try {
                        const version = await nativeHost.GetHostVersion(); // Early test call
                        setStatus(`Backend connected. Host version: ${version}. Loading devices...`, 'info');
                        await loadAudioDevices(); // This is where mic permission might fail
                        await loadSettings(); 
                    } catch (hostError) { // Catch errors from nativeHost calls specifically
                        console.error('Error during C# host operations:', hostError);
                        // A common error message for GetMicrophoneDevices failing due to permissions
                        // might include "Access is denied" or similar. This is a guess.
                        // More robust error handling would inspect hostError.message or have C# return specific error codes.
                        if (hostError && hostError.message && hostError.message.toLowerCase().includes('access is denied')) {
                            setStatus('Microphone permission denied by the system.', 'error');
                        } else {
                            setStatus(`Error initializing app components: ${hostError.message || hostError}`, 'error');
                        }
                        if(saveButton) saveButton.disabled = true; // Disable save if essential setup failed
                        if(microphoneSelect) microphoneSelect.innerHTML = '<option value="">Mic access error</option>';
                        // Potentially disable other controls too
                    }
                } else {
                    setStatus('Error: Backend host object (nativeHost) resolved to null. Running in limited mode.', 'error');
                    if(saveButton) saveButton.disabled = true;
                }
            } else {
                setStatus('Error: WebView2 host objects API not found. Running in limited mode.', 'error');
                if(saveButton) saveButton.disabled = true;
            }
        } catch (error) {
            setStatus(`Fatal error connecting to backend: ${error.message}. Running in limited mode.`, 'error');
            if(saveButton) saveButton.disabled = true;
            console.error('Error during nativeHost initialization or GetHostVersion call:', error);
            if(microphoneSelect) microphoneSelect.innerHTML = '<option value="">Error connecting to backend</option>';
        }
    }

    async function loadAudioDevices() {
        if (!nativeHost) {
            setStatus('Cannot load devices: Backend not connected.', 'error');
            microphoneSelect.innerHTML = '<option value="">Backend not connected</option>';
            return;
        }
        try {
            setStatus('Loading audio devices from device...', 'info');
            microphoneSelect.innerHTML = '<option value="" disabled selected>Loading...</option>';
            const devices = await nativeHost.GetMicrophoneDevices(); 

            microphoneSelect.innerHTML = ''; // Clear loading
            if (devices && devices.length > 0) {
                devices.forEach(deviceName => { // Assuming C# returns string[] for now
                    const option = document.createElement('option');
                    option.value = deviceName; 
                    option.textContent = deviceName;
                    microphoneSelect.appendChild(option);
                });
                setStatus('Audio devices loaded from C#.', 'success');
            } else {
                microphoneSelect.innerHTML = '<option value="">No capture devices found (C#)</option>';
                setStatus('No audio capture devices found (from C#).', 'info');
            }
        } catch (error) {
            console.error('Error loading audio devices from device:', error);
            microphoneSelect.innerHTML = '<option value="">Error loading devices (device)</option>';
            setStatus(`Error loading devices from device: ${error.message || error}`, 'error');
        }
    }

    // Debounced handler for volume slider input
    const debouncedVolumeUpdate = debounce((value) => {
        if (nativeHost && enforceCheckbox.checked) {
            const selectedDeviceId = microphoneSelect.value;
            if (selectedDeviceId) {
                // Call SetEnforcement to update backend immediately for responsiveness of enforcement state
                // The C# side handles its own cooldown for actual volume setting during OnVolumeNotification
                nativeHost.SetEnforcement(selectedDeviceId, parseInt(value), true)
                    .then(() => {
                        // We might not need a status update here if handleVolumeChange does it on 'change'
                        // setStatus(`Enforcement target updated to ${value}% for ${selectedDeviceId}.`, 'info');
                    })
                    .catch(error => {
                        console.error('Error updating enforcement target via C# (debounced): ', error);
                        setStatus(`Error updating enforcement target: ${error.message || error}`, 'error');
                    });
            }
        }
    }, 250); // 250ms debounce delay, adjust as needed

    // Original handleVolumeChange for the 'change' event (fires when user releases slider)
    async function handleVolumeChange() {
        const volume = volumeSlider.value;
        // aria-valuenow is already updated by the 'input' event listener
        if (enforceCheckbox.checked && nativeHost) {
            const selectedDeviceId = microphoneSelect.value;
            if (selectedDeviceId) {
                try {
                    console.log(`JS: (commit) Enforcing volume for ${selectedDeviceId} to ${volume}%`);
                    // This call to SetMicrophoneVolume is fine as it reflects final decision
                    await nativeHost.SetMicrophoneVolume(selectedDeviceId, parseInt(volume));
                    // Also ensure enforcement state is synced with this final volume
                    await nativeHost.SetEnforcement(selectedDeviceId, parseInt(volume), true);
                    setStatus(`Volume for ${selectedDeviceId} set to ${volume}%.`, 'success');
                } catch (error) {
                    console.error('Error setting volume via C# (on change):', error);
                    setStatus(`Error setting volume: ${error.message || error}`, 'error');
                }
            }
        }
    }

    async function toggleEnforcement() {
        const selectedDeviceId = microphoneSelect.value;
        const volume = volumeSlider.value;
        if (enforceCheckbox.checked && !selectedDeviceId) {
            setStatus('Please select a microphone to enforce volume.', 'warning');
            enforceCheckbox.checked = false;
            return;
        }
        if (nativeHost && selectedDeviceId) {
            try {
                await nativeHost.SetEnforcement(selectedDeviceId, parseInt(volume), enforceCheckbox.checked);
                if (enforceCheckbox.checked) {
                    console.log(`JS: Enforcement ON for ${selectedDeviceId} at ${volume}%`);
                    setStatus(`Enforcement enabled for ${selectedDeviceId}.`, 'success');
                } else {
                    setStatus('Volume enforcement disabled.', 'info');
                    console.log('JS: Enforcement OFF');
                }
            } catch (error) {
                console.error('Error toggling enforcement via C#:', error);
                setStatus(`Error toggling enforcement: ${error.message || error}`, 'error');
            }
        }
    }

    async function saveSettings() {
        if (!nativeHost) {
            setStatus('Cannot save settings: Backend not connected.', 'error');
            console.error('JS: nativeHost is null');
            return;
        }

        // Safety checks for all elements
        if (!microphoneSelect) {
            console.error('JS: microphoneSelect element not found');
            return;
        }
        if (!volumeSlider) {
            console.error('JS: volumeSlider element not found');
            return;
        }
        if (!enforceCheckbox) {
            console.error('JS: enforceCheckbox element not found');
            return;
        }
        if (!startWithWindowsCheckbox) {
            console.error('JS: startWithWindowsCheckbox element not found');
            return;
        }
        if (!closeBehaviorSelect) {
            console.error('JS: closeBehaviorSelect element not found');
            return;
        }
        if (!dontAskAgainCheckbox) {
            console.error('JS: dontAskAgainCheckbox element not found');
            return;
        }

        const settings = {
            selectedDevice: microphoneSelect.value,
            targetVolume: parseInt(volumeSlider.value),
            isEnforced: enforceCheckbox.checked,
            startWithWindows: startWithWindowsCheckbox.checked,
            closeBehavior: closeBehaviorSelect.value,
            dontAskAgain: dontAskAgainCheckbox.checked,
        };
        try {
            console.log('JS: Saving settings:', settings);
            console.log('JS: Settings JSON being sent:', JSON.stringify(settings));
            await nativeHost.SaveSettings(JSON.stringify(settings));
            setStatus('Settings saved successfully.', 'success');
            console.log('JS: Settings saved successfully to C# backend');
        } catch (error) {
            console.error('Error saving settings to device:', error);
            setStatus(`Error saving settings: ${error.message || error}`, 'error');
        }
    }

    async function loadSettings() {
        if (!nativeHost) {
            console.log('JS: Cannot load settings, backend not connected yet.');
            return;
        }
        try {
            setStatus('Loading settings from device...', 'info');
            const settingsJson = await nativeHost.LoadSettings();
            let settings = {};

            if (settingsJson) {
                settings = JSON.parse(settingsJson);
                console.log('JS: Loaded settings from JSON:', settings);
            }

            // Handle Start with Windows (sync with registry as source of truth)
            if (startWithWindowsCheckbox && nativeHost.GetStartWithWindows) {
                try {
                    const registryStartsWithWin = await nativeHost.GetStartWithWindows();
                    startWithWindowsCheckbox.checked = registryStartsWithWin;
                    if (settings.startWithWindows !== registryStartsWithWin) {
                        console.log('JS: Startup setting mismatch (JSON vs Registry), updating JSON.');
                        settings.startWithWindows = registryStartsWithWin; // Update settings object
                        // No immediate save here, will be saved if other settings are processed or explicitly by user
                    }
                } catch (e) {
                    console.error("Error getting startup state from C#: ", e);
                    setStatus("Error loading startup setting.", "error");
                }
            } else if (typeof settings.startWithWindows === 'boolean' && startWithWindowsCheckbox) {
                 // Fallback if GetStartWithWindows is not available or fails, use JSON value
                startWithWindowsCheckbox.checked = settings.startWithWindows;
            }

            // Apply other settings from JSON
            if (settings.selectedDevice && microphoneSelect.options.length > 0) { 
                microphoneSelect.value = settings.selectedDevice;
            }
            if (settings.targetVolume) {
                volumeSlider.value = settings.targetVolume;
                volumeDisplay.textContent = `${settings.targetVolume}%`;
                volumeSlider.setAttribute('aria-valuenow', settings.targetVolume);
            }
            if (typeof settings.isEnforced === 'boolean') {
                enforceCheckbox.checked = settings.isEnforced;
                if (nativeHost && settings.selectedDevice && settings.targetVolume) {
                    try {
                        await nativeHost.SetEnforcement(settings.selectedDevice, parseInt(settings.targetVolume), settings.isEnforced);
                        // Simplified status messages based on previous edits
                        setStatus(settings.isEnforced ? `Enforcement for ${settings.selectedDevice} re-initiated.` : `Enforcement off for ${settings.selectedDevice}.`, 'info');
                    } catch (error) {
                        setStatus(`Error syncing enforcement: ${error.message || error}`, 'error');
                    }
                }
            }
            if (settings.closeBehavior && closeBehaviorSelect) {
                closeBehaviorSelect.value = settings.closeBehavior;
                closeBehaviorSelect.dispatchEvent(new Event('change')); 
            }
            if (typeof settings.dontAskAgain === 'boolean' && dontAskAgainCheckbox) {
                dontAskAgainCheckbox.checked = settings.dontAskAgain;
            }
            
            // Only report full load if settings were actually processed
            if (Object.keys(settings).length > 0 || settingsJson) { 
                 setStatus('Settings loaded and applied.', 'success');
            } else if (!settingsJson) {
                 setStatus('No saved settings found.', 'info');
            }

        } catch (error) {
            console.error('Error loading/parsing settings:', error);
            setStatus(`Error loading settings: ${error.message || error}`, 'error');
        }
    }
    
    function setStatus(message, type = 'info') {
        let statusEl = document.getElementById('status-message');
        const container = statusMessageContainer;

        if (!container) {
            console.error("Status message container not found!");
            return;
        }

        if (statusClearTimer) {
            clearTimeout(statusClearTimer);
            statusClearTimer = null;
        }

        if (!statusEl) {
            statusEl = document.createElement('div');
            statusEl.id = 'status-message';
            statusEl.setAttribute('aria-live', 'polite');
            statusEl.setAttribute('role', 'status');
            const firstControlSection = container.querySelector('.control-section');
            if (firstControlSection) {
                container.insertBefore(statusEl, firstControlSection);
            } else {
                container.appendChild(statusEl);
            }
        } else {
            statusEl.style.display = 'block';
        }
        
        statusEl.textContent = message;
        statusEl.className = 'status ' + type;

        console.log(`Status (${type}): ${message}`);

        if (type === 'success') {
            statusClearTimer = setTimeout(() => {
                const currentStatusEl = document.getElementById('status-message');
                if (currentStatusEl && currentStatusEl.textContent === message) { 
                    currentStatusEl.remove();
                }
            }, 4000); 
        } else if (type !== 'info' && type !== 'warning' && type !=='error') {
            // For other types not explicitly success, error, warning, info - remove after a longer delay perhaps or not at all
            // For now, only success auto-hides by removal. Others persist.
        }
    }

    if (refreshButton) {
        refreshButton.addEventListener('click', async () => {
            setStatus('Refreshing audio devices...', 'info');
            await loadAudioDevices();
            await loadSettings(); 
        });
    }
    if (volumeSlider) {
        volumeSlider.addEventListener('input', () => {
            const currentValue = volumeSlider.value;
            volumeDisplay.textContent = `${currentValue}%`;
            volumeSlider.setAttribute('aria-valuenow', currentValue);
            // Call the debounced function for live updates if enforcement is on
            if (enforceCheckbox.checked) {
                debouncedVolumeUpdate(currentValue);
            }
        });
        // 'change' event fires when the user releases the mouse or finishes interaction
        volumeSlider.addEventListener('change', () => {
            handleVolumeChange();
            saveSettings(); // Auto-save when volume changes
        }); 
    }
    if (enforceCheckbox) {
        enforceCheckbox.addEventListener('change', () => {
            toggleEnforcement();
            saveSettings(); // Auto-save when enforcement changes
        });
    }
    if (saveButton) {
        saveButton.addEventListener('click', saveSettings);
    }
    if(microphoneSelect) {
        microphoneSelect.addEventListener('change', () => {
            if(enforceCheckbox.checked) {
                if (nativeHost) {
                    nativeHost.SetEnforcement(microphoneSelect.value, parseInt(volumeSlider.value), true);
                }
                handleVolumeChange(); 
            }
            saveSettings(); // Auto-save when microphone selection changes
        });
    }

    if (closeBehaviorSelect) {
        closeBehaviorSelect.addEventListener('change', () => {
            if (askOnceGroup) {
                askOnceGroup.style.display = closeBehaviorSelect.value === 'ask' ? 'block' : 'none';
            }
            saveSettings();
        });
        if (askOnceGroup && closeBehaviorSelect.value === 'ask') {
            askOnceGroup.style.display = 'block';
        }
    }

    if(startWithWindowsCheckbox) {
        startWithWindowsCheckbox.addEventListener('change', async () => {
            if (nativeHost && nativeHost.SetStartWithWindows) {
                try {
                    await nativeHost.SetStartWithWindows(startWithWindowsCheckbox.checked);
                    setStatus(`Start with Windows ${startWithWindowsCheckbox.checked ? 'enabled' : 'disabled'} (Registry updated).`, 'info'); 
                } catch (e) {
                    console.error("Error setting startup state via C#: ", e);
                    setStatus("Error updating startup registry.", "error");
                    // Optionally revert checkbox UI if C# call failed, though C# Get should fix on next load
                }
            } 
            saveSettings(); // Save all UI settings (including this one) to JSON
        });
    }
    if(dontAskAgainCheckbox) {
        dontAskAgainCheckbox.addEventListener('change', saveSettings);
    }

    // Function to apply theme based on 'data-theme' attribute
    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        if (sunSvgIconInstance && moonSvgIconInstance) {
            if (theme === 'dark') {
                sunSvgIconInstance.style.display = 'inline-block';
                moonSvgIconInstance.style.display = 'none';
            } else {
                sunSvgIconInstance.style.display = 'none';
                moonSvgIconInstance.style.display = 'inline-block';
            }
        }
        localStorage.setItem('theme', theme);
    }

    // Function to initialize theme from localStorage or prefers-color-scheme
    function initializeTheme() {
        const savedTheme = localStorage.getItem('theme');
        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        if (savedTheme) {
            applyTheme(savedTheme);
        } else if (prefersDark) {
            applyTheme('dark');
        } else {
            applyTheme('light'); // Default to light if no preference
        }
    }

    if (themeToggleButton) {
        themeToggleButton.addEventListener('click', () => {
            const currentTheme = document.documentElement.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            applyTheme(newTheme);
        });
        themeToggleButton.setAttribute('tabindex', '-1');
    }

    if (settingsToggleButton) {
        settingsToggleButton.addEventListener('click', () => {
            if (mainView && settingsView) {
                mainView.style.display = 'none';
                settingsView.style.display = 'block';
            }
        });
    }

    if (backToMainButton) {
        backToMainButton.addEventListener('click', () => {
            if (mainView && settingsView) {
                settingsView.style.display = 'none';
                mainView.style.display = 'block';
            }
        });
    }

    // Global keydown listener for theme toggle hotkey (Alt+T)
    window.addEventListener('keydown', (event) => {
        if (event.altKey && event.key === 't') {
            event.preventDefault(); // Prevent any default browser action for Alt+T
            const currentTheme = document.documentElement.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            applyTheme(newTheme);
        }
    });

    // Initialize theme on load
    initializeTheme();

    // Add global test functions for debugging
    window.testSaveSettings = saveSettings;
    window.testLoadSettings = loadSettings;
    window.testGetCloseBehavior = () => {
        if (closeBehaviorSelect) {
            console.log('Current closeBehavior value:', closeBehaviorSelect.value);
            return closeBehaviorSelect.value;
        } else {
            console.error('closeBehaviorSelect not found');
            return null;
        }
    };

    initializeApp();
}); 