using System.Collections.Generic;
using DataSense.Core.Domain;

namespace DataSense.Core.Interfaces
{
    public interface INetworkInterfaceService
    {
        IEnumerable<NetworkAdapterInfo> GetAvailableAdapters();
        IEnumerable<string> GetLocalIpAddresses();
    }
}
