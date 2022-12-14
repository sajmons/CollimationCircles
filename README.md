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

![image](https://user-images.githubusercontent.com/7437280/207387640-f0b2f880-c2d1-4462-a083-bab68d465b8d.png)

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

# Collimation process

## 1. Preparations

For collimation circles app to help you with collimation process you'll need this:

- camera (webcam, phone or CMOS) attached to the focuser
  - I was using phone, holder to attach phone to focuser, DroidCam app from Play Store to stream live view to computer browser
- computer displaying live image from webcam, phone or CMOS
- Collimation Circles app to overlay over live stream
- screwdriver
- sheet of paper to block primary mirror, and second colored (green in my case) sheet for better contrast
- marked primary mirror center spot
- reflective collimation cap
  - I made mine from 35mm film canister with hole in the center of the cap. Then I glued shinny washer to the bottom of the cap and glue everything back to canister  

I recomend you to read excelent [AstroBaby Collimation tutorial](https://www.astro-baby.com/astrobaby/help/collimation-guide-newtonian-reflector/). Images below are generated following procedure described in the tutorial.

## 2. Align secondary mirror to center of focuser tube

![image](https://user-images.githubusercontent.com/7437280/207791142-3c5f99d5-98b9-4dd0-92c8-32a19a7d9906.png)

In this stage our goal is to center secondary mirror to center of focuser tube. Image shows that secondary mirror is not at exact center of focuser tube. In this stage it's very important to block primary mirror reflection as described in tutorial.

## 3. Align secondary mirror to primary mirror

After first step you will probably see something like this:

![image](https://user-images.githubusercontent.com/7437280/207792834-0d186ee0-675d-4599-b5ac-d83b58a2ab63.png)

From that image you can see that secondary mirror and primary are not aligned. Great method to align secondary mirror is, to tilt it until you get all primary holder clips into view (as shown in next step).

Remember to tighten all the screws on secondary mirror and check live image again.

## 4. Primary mirror alignment

Last step is to align primary mirror to the optical center as close as possible. Image below shows really bad primary mirror alignment.

![image](https://user-images.githubusercontent.com/7437280/207796654-28616139-89ff-41ab-a418-be82b8d9babd.png)

In this step you unscrew locking screws of primary mirror and using collimation screws to align primary mirror center to center of reflective collimation cap. When finished you should see something like that.

![image](https://user-images.githubusercontent.com/7437280/207796904-cb43878d-f159-4073-aa27-b4d0ad527794.png)

## 5. Star test

Final stage is to perform star test (as described in tutorial) and make some final tunning of primary mirror.

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

# Images
<img src="https://user-images.githubusercontent.com/7437280/208867646-7b6d1bfe-7e5f-43b0-bfd7-6b0e3fa0c35d.png" height="200">&nbsp;<img src="https://user-images.githubusercontent.com/7437280/208643785-17b1460f-667d-4dd6-9172-5b57da3a6d44.png" height="200">&nbsp;<img src="https://user-images.githubusercontent.com/7437280/208879028-0598352c-82e1-4c58-b43b-262e6a011d21.png" height="200">
