using System;
using System.Linq;
using CoreAudio;
using Topshelf;

namespace MicrophoneVolumeEnforcer
{
    public class VolumeEnforcerService
    {
        private MMDeviceEnumerator _deviceEnumerator;

        public VolumeEnforcerService()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
        }

        public bool Start()
        {
            Console.WriteLine("Service started...");
            try
            {
                var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
                if (devices.Count == 0)
                {
                    Console.WriteLine("No active recording devices found.");
                    return false;
                }
                foreach (var device in devices)
                {
                    if (device.AudioEndpointVolume != null)
                    {
                        MonitorDevice(device);
                    }
                    else
                    {
                        Console.WriteLine($"AudioEndpointVolume is null for '{device.DeviceFriendlyName}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during service start: {ex.Message}");
                return false;
            }
            return true;
        }

        private static void MonitorDevice(MMDevice device)
        {
            if (device.AudioEndpointVolume != null)
            {
                device.AudioEndpointVolume.OnVolumeNotification += (data) =>
                {
                    float currentVolume = data.MasterVolume * 100;
                    if (currentVolume < 100)
                    {
                        SetDeviceVolume(device);
                        Console.WriteLine($"[{DateTime.Now}] Corrected volume for '{device.DeviceFriendlyName}' to 100%.");
                    }
                };
                SetDeviceVolume(device);
                Console.WriteLine($"Monitoring '{device.DeviceFriendlyName}'...");
            }
            else
            {
                Console.WriteLine($"AudioEndpointVolume is null for '{device.DeviceFriendlyName}'.");
            }
        }

        private static void SetDeviceVolume(MMDevice device)
        {
            if (device.AudioEndpointVolume != null)
            {
                try
                {
                    float currentVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
                    if (currentVolume < 100)
                    {
                        device.AudioEndpointVolume.MasterVolumeLevelScalar = 1.0f;
                        Console.WriteLine($"[{DateTime.Now}] Set volume for '{device.DeviceFriendlyName}' to 100%.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting volume for '{device.DeviceFriendlyName}': {ex.Message}");
                }
            }
        }

        public bool Stop()
        {
            Console.WriteLine("Service stopped.");
            return true;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<VolumeEnforcerService>(s =>
                {
                    s.ConstructUsing(name => new VolumeEnforcerService());
                    s.WhenStarted(v => v.Start());
                    s.WhenStopped(v => v.Stop());
                });
                x.RunAsLocalSystem();
                x.SetDescription("Microphone Volume Enforcer Service");
                x.SetDisplayName("Microphone Volume Enforcer");
                x.SetServiceName("MicrophoneVolumeEnforcer");
            });
        }
    }
}
