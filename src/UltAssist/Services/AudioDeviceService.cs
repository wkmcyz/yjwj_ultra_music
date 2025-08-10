using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Linq;

namespace UltAssist.Services
{
    public sealed class AudioDeviceService
    {
        private readonly MMDeviceEnumerator enumerator = new();

        public List<MMDevice> GetRenderDevices()
        {
            return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
        }

        public MMDevice GetDeviceByIdOrDefault(string id, DataFlow flow)
        {
            var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active).ToList();
            var found = devices.FirstOrDefault(d => d.ID == id);
            return found ?? enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
        }
    }
}

