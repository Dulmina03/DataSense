namespace DataSense.Core.Interfaces
{
    public interface IProcessConnectionMapper
    {
        int GetProcessId(int localPort, string protocol);
        string GetProcessName(int processId);
    }
}
