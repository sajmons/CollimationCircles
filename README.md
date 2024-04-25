# Collimation Circles

<img src="https://github.com/sajmons/CollimationCircles/assets/7437280/1424b10e-b81e-483d-bd1b-cf2ce30869a5" width="500">
<img src="https://github.com/sajmons/CollimationCircles/assets/7437280/2495c622-4683-4d8e-b39d-667203580c19" width="500">

This program was inspired by Mire De Collimation program written by Gilbert Grillot and Al's Collimation Aid. I combined best features of both and addes some of my own. Purpose of this program is not to reinvent the wheel, but rather to learn new technologies, become better at colimating my telescope and to learn something new.

Main purpose of this program is to help you with aligning optical elements of your telescope such as secondary mirror, primary mirror, focuser, etc.

Collimation Circles is developed with .NET 7 and AvaloniaUI Framework using MVVM architecture patern. Program was tested on Windows 10 and 11, Ununtu Linux 22.04.1 LTS (Wayland), Raspberry PI OS Bullseye and Bookworm. I'm not able to test it on macOS (only in virtual machine), but it should work.

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
Here are prebuild binary files avaliable for you to download (win-x64, linux-x64, linux-arm64 and macos-x64).
https://github.com/sajmons/CollimationCircles/releases/

Download latest release as ZIP file, extract it and run executable.

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
dotnet run
```

# Building and publishing

## Prerequisites for building
To use this application, you must first install .NET Framework on your computer.

## .NET Framework Instalation

### Windows 10 and above
https://learn.microsoft.com/en-us/dotnet/core/install/windows?tabs=net70

```
winget install Microsoft.DotNet.SDK.7
```

### Ubuntu 22.04 and above
https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-2204

```
sudo apt-get update && sudo apt-get install -y dotnet-sdk-7.0
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
sudo apt-get update && sudo apt-get install -y dotnet-sdk-7.0
```

### Raspbian OS Bullseye ARM
https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian
```
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
```
```
sudo chmod +x ./dotnet-install.sh
```
```
sudo ./dotnet-install.sh --channel 7.0 --install-dir /opt/dotnet/
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
https://learn.microsoft.com/en-us/dotnet/core/install/macos

## Build and publish on Windows

On windows I'm using these commands to make prebuild binaries.
```
dotnet restore .\CollimationCircles.sln -r win-x64
```
```
dotnet publish -c Release -f net7.0 -r win-x64 -o D:\Projects\Publish\CC\win-64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
```
For more on building see https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish.
