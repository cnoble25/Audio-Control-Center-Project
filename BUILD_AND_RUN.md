# How to Build and Run Audio Control Center

## Prerequisites
- .NET 9.0 SDK (already installed ✅)
- Visual Studio 2022 with .NET MAUI workload (for Visual Studio method)
- Windows 10/11 (for Windows target)

## Method 1: Using Visual Studio (Easiest)

1. **Open the Solution**
   - Navigate to: `Audio Control Center Application\Audio Control Center Application.sln`
   - Double-click to open in Visual Studio 2022

2. **Select Target Framework**
   - In the toolbar, choose: `net9.0-windows10.0.19041.0` (Windows)
   - Select `Debug` or `Release` configuration

3. **Run the Application**
   - Press `F5` or click the green ▶️ Start button
   - Or: Debug → Start Debugging

## Method 2: Using Command Line

### Navigate to Project Directory
```powershell
cd "E:\Audio Control Center Project\Audio-Control-Center-Project\Audio Control Center Application"
```

### Restore NuGet Packages
```powershell
dotnet restore
```

### Build the Project (Debug)
```powershell
dotnet build -f net9.0-windows10.0.19041.0 -c Debug
```

### Run the Application
```powershell
dotnet run -f net9.0-windows10.0.19041.0 -c Debug
```

### Build for Release
```powershell
dotnet build -f net9.0-windows10.0.19041.0 -c Release
```

## Quick Build & Run Command (All-in-One)
```powershell
cd "E:\Audio Control Center Project\Audio-Control-Center-Project\Audio Control Center Application"
dotnet run -f net9.0-windows10.0.19041.0
```

## Troubleshooting

### If you get "Target framework not found" error:
- Make sure you have .NET 9.0 SDK installed
- Check with: `dotnet --list-sdks`

### If you get build errors:
- Restore packages: `dotnet restore`
- Clean and rebuild: `dotnet clean` then `dotnet build`

### If COM port errors occur:
- Open Settings (⚙️ button) in the app
- Select the correct COM port
- Verify baud rate matches your device

## Project Output Location
- Debug: `bin\Debug\net9.0-windows10.0.19041.0\win10-x64\Audio Control Center Application.exe`
- Release: `bin\Release\net9.0-windows10.0.19041.0\win10-x64\Audio Control Center Application.exe`
