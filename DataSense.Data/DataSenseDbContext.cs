using Microsoft.EntityFrameworkCore;
using System.IO;
using System;
using DataSense.Data.Entities;

namespace DataSense.Data
{
    public class DataSenseDbContext : DbContext
    {
        public DbSet<NetworkAdapterEntity> Adapters { get; set; }
        public DbSet<ProcessUsageEntity> ProcessUsages { get; set; }
        public DbSet<DailyUsageEntity> DailyUsages { get; set; }

        public DataSenseDbContext(DbContextOptions<DataSenseDbContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        public DataSenseDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dbPath = Path.Combine(appData, "DataSense", "datasense.db");
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
                
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProcessUsageEntity>()
                .HasIndex(p => new { p.ProcessName, p.Date }).IsUnique();

            modelBuilder.Entity<DailyUsageEntity>()
                .HasIndex(d => d.Date).IsUnique();
        }
    }
}
