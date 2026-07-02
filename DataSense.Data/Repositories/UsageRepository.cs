using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataSense.Core.Domain;
using DataSense.Core.Repositories;
using DataSense.Data.Entities;

namespace DataSense.Data.Repositories
{
    public class UsageRepository : IUsageRepository
    {
        private readonly DataSenseDbContext _context;

        public UsageRepository(DataSenseDbContext context)
        {
            _context = context;
        }

        public async Task SaveUsageAsync(DateTime date, long bytesDownloaded, long bytesUploaded, Dictionary<string, UsageStats> processUsages)
        {
            var dateOnly = date.Date;

            // Update daily usage
            var dailyUsage = await _context.DailyUsages.FirstOrDefaultAsync(d => d.Date == dateOnly);
            if (dailyUsage == null)
            {
                dailyUsage = new DailyUsageEntity { Date = dateOnly };
                _context.DailyUsages.Add(dailyUsage);
            }
            dailyUsage.BytesDownloaded += bytesDownloaded;
            dailyUsage.BytesUploaded += bytesUploaded;

            // Update process usages
            var processNames = processUsages.Keys.ToList();
            var existingProcessUsages = await _context.ProcessUsages
                .Where(p => p.Date == dateOnly && processNames.Contains(p.ProcessName))
                .ToDictionaryAsync(p => p.ProcessName);

            foreach (var pu in processUsages)
            {
                if (existingProcessUsages.TryGetValue(pu.Key, out var entity))
                {
                    entity.BytesDownloaded += pu.Value.BytesDownloaded;
                    entity.BytesUploaded += pu.Value.BytesUploaded;
                }
                else
                {
                    _context.ProcessUsages.Add(new ProcessUsageEntity
                    {
                        Date = dateOnly,
                        ProcessName = pu.Key,
                        BytesDownloaded = pu.Value.BytesDownloaded,
                        BytesUploaded = pu.Value.BytesUploaded
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<UsageStats> GetTotalUsageForMonthAsync(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var usages = await _context.DailyUsages
                .Where(d => d.Date >= start && d.Date < end)
                .ToListAsync();

            return new UsageStats
            {
                BytesDownloaded = usages.Sum(u => u.BytesDownloaded),
                BytesUploaded = usages.Sum(u => u.BytesUploaded)
            };
        }

        public async Task<List<ProcessUsageInfo>> GetProcessUsagesForDateAsync(DateTime date)
        {
            var dateOnly = date.Date;
            var entities = await _context.ProcessUsages
                .Where(p => p.Date == dateOnly)
                .ToListAsync();

            return entities.Select(e => new ProcessUsageInfo
            {
                ProcessName = e.ProcessName,
                Stats = new UsageStats
                {
                    BytesDownloaded = e.BytesDownloaded,
                    BytesUploaded = e.BytesUploaded
                }
            }).ToList();
        }
        public async Task<List<DailyUsageInfo>> GetDailyUsagesAsync(DateTime from, DateTime to)
        {
            var entities = await _context.DailyUsages
                .Where(d => d.Date >= from.Date && d.Date <= to.Date)
                .OrderBy(d => d.Date)
                .ToListAsync();

            return entities.Select(e => new DailyUsageInfo
            {
                Date = e.Date,
                BytesDownloaded = e.BytesDownloaded,
                BytesUploaded = e.BytesUploaded
            }).ToList();
        }

        public async Task<List<ProcessUsageInfo>> GetProcessUsagesForMonthAsync(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var groups = await _context.ProcessUsages
                .Where(p => p.Date >= start && p.Date < end)
                .GroupBy(p => p.ProcessName)
                .Select(g => new
                {
                    ProcessName = g.Key,
                    TotalDl = g.Sum(x => x.BytesDownloaded),
                    TotalUl = g.Sum(x => x.BytesUploaded)
                })
                .OrderByDescending(g => g.TotalDl + g.TotalUl)
                .ToListAsync();

            return groups.Select(g => new ProcessUsageInfo
            {
                ProcessName = g.ProcessName,
                Stats = new UsageStats
                {
                    BytesDownloaded = g.TotalDl,
                    BytesUploaded = g.TotalUl
                }
            }).ToList();
        }

        public async Task<List<ProcessUsageInfo>> GetProcessUsagesForPeriodAsync(DateTime from, DateTime to)
        {
            var start = from.Date;
            var end = to.Date.AddDays(1); // include the end date fully

            var groups = await _context.ProcessUsages
                .Where(p => p.Date >= start && p.Date < end)
                .GroupBy(p => p.ProcessName)
                .Select(g => new
                {
                    ProcessName = g.Key,
                    TotalDl = g.Sum(x => x.BytesDownloaded),
                    TotalUl = g.Sum(x => x.BytesUploaded)
                })
                .OrderByDescending(g => g.TotalDl + g.TotalUl)
                .ToListAsync();

            return groups.Select(g => new ProcessUsageInfo
            {
                ProcessName = g.ProcessName,
                Stats = new UsageStats
                {
                    BytesDownloaded = g.TotalDl,
                    BytesUploaded = g.TotalUl
                }
            }).ToList();
        }
    }
}
