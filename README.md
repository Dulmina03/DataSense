# DataSense

DataSense is a Windows desktop application for monitoring and analyzing network data usage in real time.

It tracks download and upload activity, monitors data usage by application, stores usage history, and provides visual statistics to help users understand how their internet connection is being used.

## Features

* Real-time download and upload speed monitoring
* Daily, weekly, and monthly data usage tracking
* Per-application network usage tracking
* Network usage history
* Custom time-period usage analysis
* Download and upload usage charts
* Monthly data limit monitoring
* Data usage alerts at 50%, 75%, 90%, and 100% of the configured limit
* Built-in internet speed test

  * Ping
  * Jitter
  * Download speed
  * Upload speed
* Network connection information
* Network adapter detection
* Wi-Fi/network name detection
* Net speed meter
* System tray support
* Start with Windows option
* Dark theme support
* Usage data export support

## Technologies Used

* C#
* .NET 8
* WPF
* Entity Framework Core
* SQLite
* Npcap / SharpPcap
* CommunityToolkit.Mvvm
* LiveCharts2
* Material Design for WPF
* CsvHelper
* PDFsharp / MigraDoc
* Serilog

## Project Structure

The solution is separated into multiple projects:

```text
DataSense/
│
├── DataSense.Core/
│   ├── Domain/
│   ├── Interfaces/
│   ├── Repositories/
│   └── Services/
│
├── DataSense.Data/
│   ├── Entities/
│   ├── Repositories/
│   └── DataSenseDbContext.cs
│
├── DataSense.Infrastructure/
│   └── Network/
│
├── DataSense.UI/
│   ├── ViewModels/
│   ├── Services/
│   ├── Converters/
│   └── WPF windows and UI files
│
├── installer/
│
└── DataSense.sln
```

### DataSense.Core

Contains the main application logic, domain models, interfaces, network usage aggregation, data-limit alerts, network monitoring, and speed testing.

### DataSense.Data

Handles persistent data storage using Entity Framework Core and SQLite.

It stores information such as:

* Daily network usage
* Application/process usage
* Network adapter usage
* Historical usage data

### DataSense.Infrastructure

Contains the Windows-specific network monitoring implementation.

It uses packet capture and Windows process information to associate network traffic with applications.

### DataSense.UI

Contains the WPF graphical user interface, ViewModels, charts, history views, export functionality, system tray integration, and net speed meter.

## Requirements

To build and run DataSense, you need:

* Windows 10 or Windows 11 (64-bit)
* .NET 8 SDK
* Npcap
* Git

Npcap is required for packet capture and network traffic monitoring.

## Installation for Development

### 1. Clone the repository

```bash
git clone https://github.com/Dulmina03/DataSense.git
```

### 2. Open the project directory

```bash
cd DataSense
```

### 3. Restore dependencies

```bash
dotnet restore
```

### 4. Build the project

```bash
dotnet build
```

### 5. Run DataSense

```bash
dotnet run --project DataSense.UI
```

Make sure Npcap is installed before starting network monitoring.

## How It Works

DataSense captures network packets from available network adapters.

The captured traffic is processed and associated with running Windows applications. Downloaded and uploaded bytes are then aggregated and stored in a local SQLite database.

```text
Network Traffic
      ↓
Npcap / Packet Capture
      ↓
Process Identification
      ↓
Network Usage Aggregator
      ↓
SQLite Database
      ↓
DataSense UI
      ↓
Charts / History / Statistics
```

This allows DataSense to display both overall network usage and application-specific data consumption.

## Data Usage Monitoring

DataSense can monitor:

* Current download speed
* Current upload speed
* Daily usage
* Weekly usage
* Monthly usage
* Application-specific usage
* Usage during a custom time period

Historical usage information is stored locally using SQLite.

## Data Limit Alerts

Users can configure a monthly data limit.

DataSense can generate alerts when usage reaches:

* 50%
* 75%
* 90%
* 100%

This helps users keep track of limited internet or mobile-data connections.

## Speed Test

DataSense includes a built-in network speed test that measures:

* Ping
* Jitter
* Download speed
* Upload speed

The application also displays connection information such as the public IP address, ISP, and test location when available.

## Privacy

DataSense stores network usage information locally on the user's computer using SQLite.

The application monitors traffic statistics to calculate data usage and associate network activity with applications.

## Platform

DataSense is currently designed for:

**Windows 10 / Windows 11 (x64)**

The current implementation uses Windows-specific APIs and WPF, so it is not designed to run natively on Linux or macOS.

## License

This project is licensed under the terms included in the `LICENSE` file.

## Author

Developed by **Dulmina**

GitHub: **Dulmina03**


