using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using DataSense.Core.Domain;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace DataSense.UI.Services
{
    public class ExportService
    {
        public void ExportToCsv(string filePath, List<DailyUsageInfo> dailyUsages, List<ProcessUsageInfo> processUsages)
        {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Daily summary section
            csv.WriteField("=== Daily Usage ===");
            csv.NextRecord();
            csv.WriteField("Date");
            csv.WriteField("Downloaded");
            csv.WriteField("Uploaded");
            csv.WriteField("Total");
            csv.NextRecord();

            foreach (var day in dailyUsages)
            {
                csv.WriteField(day.Date.ToString("yyyy-MM-dd"));
                csv.WriteField(FormatBytes(day.BytesDownloaded));
                csv.WriteField(FormatBytes(day.BytesUploaded));
                csv.WriteField(FormatBytes(day.TotalBytes));
                csv.NextRecord();
            }

            csv.NextRecord();
            csv.WriteField("=== Per-Application Usage ===");
            csv.NextRecord();
            csv.WriteField("Process");
            csv.WriteField("Downloaded");
            csv.WriteField("Uploaded");
            csv.WriteField("Total");
            csv.NextRecord();

            foreach (var proc in processUsages.OrderByDescending(p => p.Stats.BytesDownloaded + p.Stats.BytesUploaded))
            {
                csv.WriteField(proc.ProcessName);
                csv.WriteField(FormatBytes(proc.Stats.BytesDownloaded));
                csv.WriteField(FormatBytes(proc.Stats.BytesUploaded));
                csv.WriteField(FormatBytes(proc.Stats.BytesDownloaded + proc.Stats.BytesUploaded));
                csv.NextRecord();
            }
        }

        public void ExportToPdf(string filePath, string reportTitle, List<DailyUsageInfo> dailyUsages, List<ProcessUsageInfo> processUsages)
        {
            var document = new Document();
            document.Info.Title = reportTitle;

            var section = document.AddSection();
            section.PageSetup.TopMargin = "2cm";
            section.PageSetup.BottomMargin = "2cm";
            section.PageSetup.LeftMargin = "2cm";
            section.PageSetup.RightMargin = "2cm";

            // Title
            var title = section.AddParagraph(reportTitle);
            title.Format.Font.Size = 20;
            title.Format.Font.Bold = true;
            title.Format.SpaceAfter = "0.5cm";

            var subtitle = section.AddParagraph($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            subtitle.Format.Font.Size = 10;
            subtitle.Format.Font.Color = Colors.Gray;
            subtitle.Format.SpaceAfter = "1cm";

            // Daily Usage Table
            section.AddParagraph("Daily Usage Summary").Format.Font.Bold = true;
            section.AddParagraph().Format.SpaceAfter = "0.3cm";

            var dailyTable = section.AddTable();
            dailyTable.Borders.Width = 0.5;
            dailyTable.AddColumn("5cm");
            dailyTable.AddColumn("4cm");
            dailyTable.AddColumn("4cm");
            dailyTable.AddColumn("4cm");

            var headerRow = dailyTable.AddRow();
            headerRow.Shading.Color = Colors.DarkSlateGray;
            headerRow.Cells[0].AddParagraph("Date").Format.Font.Color = Colors.White;
            headerRow.Cells[1].AddParagraph("Downloaded").Format.Font.Color = Colors.White;
            headerRow.Cells[2].AddParagraph("Uploaded").Format.Font.Color = Colors.White;
            headerRow.Cells[3].AddParagraph("Total").Format.Font.Color = Colors.White;

            foreach (var day in dailyUsages)
            {
                var row = dailyTable.AddRow();
                row.Cells[0].AddParagraph(day.Date.ToString("dd MMM yyyy"));
                row.Cells[1].AddParagraph(FormatBytes(day.BytesDownloaded));
                row.Cells[2].AddParagraph(FormatBytes(day.BytesUploaded));
                row.Cells[3].AddParagraph(FormatBytes(day.TotalBytes));
            }

            section.AddParagraph().Format.SpaceAfter = "0.8cm";

            // Process Usage Table
            section.AddParagraph("Per-Application Usage").Format.Font.Bold = true;
            section.AddParagraph().Format.SpaceAfter = "0.3cm";

            var procTable = section.AddTable();
            procTable.Borders.Width = 0.5;
            procTable.AddColumn("7cm");
            procTable.AddColumn("4cm");
            procTable.AddColumn("4cm");

            var procHeader = procTable.AddRow();
            procHeader.Shading.Color = Colors.DarkSlateGray;
            procHeader.Cells[0].AddParagraph("Application").Format.Font.Color = Colors.White;
            procHeader.Cells[1].AddParagraph("Downloaded").Format.Font.Color = Colors.White;
            procHeader.Cells[2].AddParagraph("Uploaded").Format.Font.Color = Colors.White;

            foreach (var proc in processUsages.Take(30))
            {
                var row = procTable.AddRow();
                row.Cells[0].AddParagraph(proc.ProcessName);
                row.Cells[1].AddParagraph(FormatBytes(proc.Stats.BytesDownloaded));
                row.Cells[2].AddParagraph(FormatBytes(proc.Stats.BytesUploaded));
            }

            var renderer = new PdfDocumentRenderer();
            renderer.Document = document;
            renderer.RenderDocument();
            renderer.PdfDocument.Save(filePath);
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
