using System;
using System.Collections.Generic;
using DataSense.Core.Domain;

namespace DataSense.Core.Interfaces
{
    public interface IPacketCaptureService : IDisposable
    {
        event EventHandler<ParsedPacket> OnPacketCaptured;
        void StartCapture(IEnumerable<string> adapterIds);
        void StopCapture();
    }
}
