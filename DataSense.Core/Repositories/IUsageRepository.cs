using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataSense.Core.Domain;

namespace DataSense.Core.Repositories
{
    public interface IUsageRepository
    {
        Task SaveUsageAsync(DateTime date, long bytesDownloaded, long bytesUploaded, Dictionary<string, UsageStats> processUsages);
        Task<UsageStats> GetTotalUsageForMonthAsync(int year, int month);
        Task<List<DailyUsageInfo>> GetDailyUsagesAsync(DateTime from, DateTime to);
        Task<List<ProcessUsageInfo>> GetProcessUsagesForDateAsync(DateTime date);
        Task<List<ProcessUsageInfo>> GetProcessUsagesForMonthAsync(int year, int month);
        Task<List<ProcessUsageInfo>> GetProcessUsagesForPeriodAsync(DateTime from, DateTime to);
    }
}
