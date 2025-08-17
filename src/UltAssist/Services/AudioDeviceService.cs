using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace UltAssist.Services
{
    public sealed class AudioDeviceService
    {
        private readonly MMDeviceEnumerator enumerator;

        public AudioDeviceService()
        {
            try
            {
                enumerator = new MMDeviceEnumerator();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"音频设备服务初始化失败: {ex.Message}", "严重错误", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                throw;
            }
        }

        public List<MMDevice> GetRenderDevices()
        {
            try
            {
                return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"获取播放设备失败: {ex.Message}", "音频错误", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return new List<MMDevice>();
            }
        }

        public List<MMDevice> GetCaptureDevices()
        {
            try
            {
                return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"获取录制设备失败: {ex.Message}", "音频错误", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return new List<MMDevice>();
            }
        }

        public MMDevice GetDeviceByIdOrDefault(string id, DataFlow flow)
        {
            try
            {
                var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active).ToList();
                var found = devices.FirstOrDefault(d => d.ID == id);
                return found ?? enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"音频设备访问失败: {ex.Message}\n\n设备ID: {id}\n流方向: {flow}", "音频错误", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                throw;
            }
        }

        public string GetDefaultEndpointId(DataFlow flow, Role role = Role.Multimedia)
        {
            return enumerator.GetDefaultAudioEndpoint(flow, role).ID;
        }

        // Try to find the capture-side partner for a given render device (e.g., VB-CABLE "CABLE Input" -> "CABLE Output").
        public string? FindLinkedCaptureId(MMDevice renderDevice)
        {
            try
            {
                var captureDevices = GetCaptureDevices();
                // Prefer exact friendly name mapping: replace "Input" with "Output"
                var targetName = renderDevice.FriendlyName.Replace("Input", "Output");
                var match = captureDevices.FirstOrDefault(d => d.FriendlyName.Equals(targetName, System.StringComparison.OrdinalIgnoreCase));
                if (match != null) return match.ID;

                // Fallback: VB-Audio naming contains "CABLE" and "Output"
                if (renderDevice.FriendlyName.Contains("CABLE", System.StringComparison.OrdinalIgnoreCase))
                {
                    match = captureDevices.FirstOrDefault(d => d.FriendlyName.Contains("CABLE", System.StringComparison.OrdinalIgnoreCase)
                                                               && d.FriendlyName.Contains("Output", System.StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match.ID;
                }
            }
            catch { }
            return null;
        }

        // Set default device via IPolicyConfig (undocumented COM). Works on Win10/11.
        public bool TrySetDefaultDevice(string deviceId)
        {
            try
            {
                var policy = (IPolicyConfig)new _PolicyConfig();
                // eMultimedia(0), eCommunications(1), eConsole(2)
                int hr1 = policy.SetDefaultEndpoint(deviceId, 0);
                int hr2 = policy.SetDefaultEndpoint(deviceId, 1);
                int hr3 = policy.SetDefaultEndpoint(deviceId, 2);
                return hr1 == 0 && hr2 == 0 && hr3 == 0;
            }
            catch { return false; }
        }

        [ComImport]
        [Guid("294935CE-F637-4E7C-A41B-AB255460B862")]
        private class _PolicyConfig { }

        [ComImport]
        [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            int Unused1();
            int Unused2();
            int Unused3();
            int Unused4();
            int Unused5();
            int Unused6();
            int Unused7();
            int Unused8();
            int Unused9();
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int role);
            // other members omitted
        }
    }
}

