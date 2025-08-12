using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace UltAssist.Services
{
    public sealed class AudioDeviceService
    {
        private readonly MMDeviceEnumerator enumerator = new();

        public List<MMDevice> GetRenderDevices()
        {
            return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
        }

        public List<MMDevice> GetCaptureDevices()
        {
            return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        }

        public MMDevice GetDeviceByIdOrDefault(string id, DataFlow flow)
        {
            var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active).ToList();
            var found = devices.FirstOrDefault(d => d.ID == id);
            return found ?? enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
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

