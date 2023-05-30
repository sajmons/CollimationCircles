# Collimation Circles

This program was inspired by Mire De Collimation program written by Gilbert Grillot and Al's Collimation Aid. I combined best features of both and addes some of my own. Purpose of this program is not to reinvent the wheel, but rather to learn new technologies, become better at colimating my telescope and to learn something new.

Main purpose of this program is to help you with aligning optical elements of your telescope such as secondary mirror, primary mirror, focuser, etc.

Collimation Circles is developed with .NET 7 and AvaloniaUI Framework using MVVM architecture patern. Program was tested on Windows 10 and 11, Ununtu Linux 22.04.1 LTS (Wayland) and Raspberry PI OS Bullseye. I'm not able to test it on macOS, but it should work.

Please be gentle. This app is still in beta stage, but It's suitable to be exposed to wider audience. Feel free to report any issues. 
Suggestions and contributions are welcome!

# Features

- support multiple helper shapes (circle, spider, screw, clip)
- user interface for managing list of shapes
- scaling up or down of whole setup
- rotation of whole setup
- transparent background
- fully customizable shapes: radius, thickness, color, spacing, rotation, label
- support for profile saving and loading (JSON files)
- precise position control with keyboard
- multiple platform support (Windows, Linux, macOS)

![image](https://github.com/sajmons/CollimationCircles/assets/7437280/ba8ada94-454c-4d6d-beaa-f90f1bf152a5)

# Download

## Prerequisite
You need to instal .NET Runtime before runing this app. Please see "Instalation and running" chapter.

## Binary files
Here are binary files avaliable for you to download.
https://github.com/sajmons/CollimationCircles/releases/

If main window is not transparent, when you run application, try runing it like this from terminal window:
```
dotnet .\CollimationCircles.dll
```

# Instalation and running

- download and install latest .NET Framework https://dotnet.microsoft.com/en-us/download
- or use Install scripts https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual#scripted-install
- Raspberry Pi https://learn.microsoft.com/en-us/dotnet/iot/deployment

Type following terminal commands:
```
git clone https://github.com/sajmons/CollimationCircles.git
cd ColiminationCircles/ColiminationCircles
dotnet run
```

# Building

For more on building see https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish.

# How to use

See this article https://saimons-astronomy.webador.com/software/collimation-circles

# Known issues

## Window transparency issues on Linux

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

## Moving window arround with arrow keys on Linux
It seams that moving window arround on Linux behaves differently zhan on Windoes. I'll try to fix that in the future.
It seams that it's avaloniaUI bug, you can see opened issue here: https://github.com/AvaloniaUI/Avalonia/issues/9972

# Images
<img src="https://user-images.githubusercontent.com/7437280/3dc6d97d-98b3-4b96-8ff9-69363ae4a352.png" height="200">&nbsp;<img src="https://user-images.githubusercontent.com/7437280/208643785-17b1460f-667d-4dd6-9172-5b57da3a6d44.png" height="200">&nbsp;<img src="https://user-images.githubusercontent.com/7437280/208879028-0598352c-82e1-4c58-b43b-262e6a011d21.png" height="200">
