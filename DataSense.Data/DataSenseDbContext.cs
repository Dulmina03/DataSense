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
        public DbSet<NetworkUsageEntity> NetworkUsages { get; set; }

        public DataSenseDbContext(DbContextOptions<DataSenseDbContext> options) : base(options)
        {
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
            var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, string>(
                v => v.ToString("yyyy-MM-dd HH:mm:ss"),
                v => DateTime.Parse(v));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                }
            }

            modelBuilder.Entity<ProcessUsageEntity>()
                .HasIndex(p => new { p.ProcessName, p.Date }).IsUnique();

            modelBuilder.Entity<DailyUsageEntity>()
                .HasIndex(d => d.Date).IsUnique();

            modelBuilder.Entity<NetworkUsageEntity>()
                .HasIndex(n => new { n.NetworkName, n.Date }).IsUnique();
        }
    }
}
