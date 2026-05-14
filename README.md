# Collimation Circles

<img src="https://github.com/sajmons/CollimationCircles/assets/7437280/1424b10e-b81e-483d-bd1b-cf2ce30869a5" width="500">
<img src="https://github.com/sajmons/CollimationCircles/assets/7437280/2495c622-4683-4d8e-b39d-667203580c19" width="500">

This application was inspired by Mire De Collimation program written by Gilbert Grillot and Al's Collimation Aid. I combined best features of both and addes some of my own. Purpose of this program is not to reinvent the wheel, but rather to learn new technologies, become better at colimating my telescope and to learn something new.

Main purpose of this program is to help you with aligning optical elements of your telescope such as secondary mirror, primary mirror, focuser, etc.

Collimation Circles is developed with .NET 10 and AvaloniaUI Framework using MVVM architecture pattern. Program was tested on Windows 10 and 11, Ubuntu Linux 22.04.1 LTS (Wayland), Raspberry PI OS Bullseye and Bookworm, and macOS arm64 (Apple Silicon).

Feel free to report any issues. Suggestions and contributions are welcome!

# Home page
https://saimons-astronomy.webador.com/software/collimation-circles

# Features

- support multiple helper shapes (circle, spider, screw, clip, bahtimov mask)
- user interface for managing list of shapes
- scaling up or down of whole setup
- rotation of whole setup
- transparent background
- fully customizable shapes: radius, thickness, color, spacing, rotation, label, inclination
- support for profile saving and loading (JSON files)
- precise position control with keyboard
- multiple platform support (Windows, Linux, macOS)
- multilanguage (English, Slovenian, German)
- always on top option
- up to date online help available
- 3.x and newer camera video stream support to display video from your telescope in background

# Dependancies
Colimation circles depends on some external software for handling video streams. 

- VLC for playing video streams.
```
sudo apt-get install -y libvlc-dev
```
- v4l-utils for detecting UVC cameras and their capabilities.
```
sudo apt-get install -y v4l-utils
```
- libcamera-vid for detecting Raspberry Pi Cameras. It should already be installed on Raspberry Pi OS by default.
  - instalation instructions https://libcamera.org/getting-started.html

# Prebuild binaries
Here are prebuild binary files available for you to download (win-x64, linux-x64, linux-arm64, osx-x64 and osx-arm64).
https://github.com/sajmons/CollimationCircles/releases/

Download the latest release, extract it and run the executable. Windows releases are packaged as ZIP files, while Linux and macOS releases are packaged as tar.gz files which preserve executable permissions.

## Instalation on MacOS
1. Open terminal application and enter this command:
```cd /Applications```
2. Download latest version from GitHub releases page https://github.com/sajmons/CollimationCircles/releases
3. Type this command in your MacOS terminal application:
```curl -LO <url address from github releases page>```
For example:
```curl -LO https://github.com/sajmons/CollimationCircles/releases/download/version-3.1.0/5-CollimationCircles-3.1.0-osx-x64.tar.gz```
4. When the download finishes, extract the archive:
```tar -xzf 5-CollimationCircles-3.1.0-osx-x64.tar.gz```
5. Remove the downloaded app quarantine attribute:
```xattr -d com.apple.quarantine CollimationCircles.app/```
6. Ensure the app bundle files are executable:
```chmod -R +x CollimationCircles.app/```
7. You should now see the CollimationCircles.app bundle. Run it with:
```open CollimationCircles.app```

## Instalation on Linux
1. Download latest version from GitHub releases page: https://github.com/sajmons/CollimationCircles/releases
2. Extract the downloaded tar.gz file:
```tar -xzf <downloaded-file>.tar.gz```
3. Run the application by double clicking on it or run this command:
```./CollimationCircles```

# How to use

Read my articles here:
- https://saimons-astronomy.webador.com/software/collimation-circles
- https://saimons-astronomy.webador.com/1191504_eaa-telescope-collimation-with-collimation-circles-application

# Known issues

### Window transparency issues on Raspberry PI OS Bullseye

Unfortunately on some Linux distros main window is not transparent :(. I have succesfully tested it on Ubuntu that's using Wayland window manager. On Raspberry Pi OS Bullseye window transparency doesn't work out of the box. But luckily there is workaround for that.

Open terminal and type this:
```
raspi-config
```
go to advanced settings and enable Compositor. Then run this command:
```
xcompmgr
```
and then run the CollimationCircles program again. Main Window should now be transparent!

Latest version of **Raspberry PI OS Bookworm** uses newer Wayland window manager and transparency works as it should.

# Running from GitHub source code (works on all platforms)
After installing .NET Framework you type following terminal commands:
```
sudo apt-get install git
```
```
git clone https://github.com/sajmons/CollimationCircles.git
```
```
cd CollimationCircles/CollimationCircles
```
```
dotnet run -f net10.0
```

# Building and publishing

## Prerequisites for building
To use this application, you must first install  Framework on your computer.

##  Framework Instalation

### Windows 10 and above
https://learn.microsoft.com/en-us/dotnet/core/install/windows

```
winget install Microsoft.DotNet.SDK.10
```

### Ubuntu 22.04 and above
https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu

```
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
```

### Raspbian OS Bullseye x64
https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian
```
wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
```
```
sudo dpkg -i packages-microsoft-prod.deb
```
```
rm packages-microsoft-prod.deb
```
```
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
```

### Raspbian OS Bullseye ARM
https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian
```
wget https://dot/v1/dotnet-install.sh -O dotnet-install.sh
```
```
sudo chmod +x ./dotnet-install.sh
```
```
sudo ./dotnet-install.sh --channel 10.0 --install-dir /opt/dotnet/
```
```
echo 'export DOTNET_ROOT=/opt/dotnet/' >> ~/.bashrc
echo 'export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools' >> ~/.bashrc
```
```
sudo reboot
```
```
dotnet --info
```

### macOS
Use Homebrew and install .NET 10 + VLC first.

1. Install Homebrew (if needed):
```
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```
2. Install required tools and runtime dependencies:
```
brew update
brew install git dotnet vlc
```
3. Verify installation:
```
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

For this project on Apple Silicon, these commands are recommended from repository root:

```
git clone https://github.com/sajmons/CollimationCircles.git
cd CollimationCircles
```

Restore/build/run:
```
dotnet restore ./CollimationCircles/CollimationCircles.csproj -r osx-arm64
dotnet build ./CollimationCircles/CollimationCircles.csproj -f net10.0
dotnet run --project ./CollimationCircles/CollimationCircles.csproj -f net10.0
```

Notes:
- On macOS arm64, the app bootstraps VLC environment variables automatically at startup.
- VLC should be installed as an app bundle (e.g. `/Applications/VLC.app`, or via Homebrew cask).
- If VLC/libVLC is missing or incompatible, the app starts in degraded mode and shows a compatibility message.

Publish a local release build (self-contained):
```
dotnet publish ./CollimationCircles/CollimationCircles.csproj \
  -c Release -f net10.0 -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=true \
  -o ./artifacts/publish/osx-arm64
```

Run published binary:
```
./artifacts/publish/osx-arm64/CollimationCircles
```

Create a tar.gz package for distribution:
```
mkdir -p ./artifacts/release
tar -czf ./artifacts/release/CollimationCircles-net10.0-osx-arm64.tar.gz -C ./artifacts/publish/osx-arm64 .
```

Optional quick check before publishing:
```
dotnet clean ./CollimationCircles/CollimationCircles.csproj
dotnet restore ./CollimationCircles/CollimationCircles.csproj -r osx-arm64
dotnet build ./CollimationCircles/CollimationCircles.csproj -c Release -f net10.0
```

## Build and publish on Windows

On windows I'm using these commands to make prebuild binaries.
```
dotnet restore .\CollimationCircles.sln -r win-x64
```
```
dotnet publish -c Release -f net10.0 -r win-x64 -o D:\Projects\Publish\CC\win-64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
```
For more on building see https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish.
