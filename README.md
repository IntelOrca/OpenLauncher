# Launcher for OpenRCT2 and OpenLoco

A launcher for automatically downloading the latest, or specific versions of [OpenRCT2](https://github.com/OpenRCT2/OpenRCT2) and [OpenLoco](https://github.com/OpenLoco/OpenLoco).

<a href="docs/launcher.png"><img src="docs/launcher.png" width="50%" /></a>

# 🚀 Installation
1. Download the latest version of the launcher from the [Releases page](https://github.com/IntelOrca/OpenLauncher/releases).
2. Save the file anywhere on your system and run it.

# 🔨 Building

**Open Launcher** is written in C# using the [AvaloniaUI](http://avaloniaui.net) framework. The application currently targets [.NET 6](https://dotnet.microsoft.com) and is typically distributed as a self contained executable.

### Prerequisites
* [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
* [Visual Studio](https://visualstudio.microsoft.com) (optional)
  * [AvaloniaUI extension](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaVS) (optional)
* [Visual Studio Code](https://code.visualstudio.com) (optional)

### Running
You can quickly build and run the launcher on the command line using the following command.
```
dotnet run --project src/openlauncher
```

Alternatively, open `openlauncher.sln` in Visual Studio. Installing the [extension](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaVS) for AvaloniaUI is recommended.


# ⚖️ Licence
**Open Launcher** is licensed under the MIT License.
