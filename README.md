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

- multiple helper shapes: circle, spider, screw, primary clip and Bahtinov mask, with ready-made presets for primary mirror, secondary mirror and focuser tube
- user interface for managing the list of shapes (add, duplicate, reorder, reset)
- fully customizable shapes: radius, thickness, color, spacing, count, rotation, inclination, label and opacity
- scaling, rotation and offset of the whole setup
- transparent, always on top overlay window with optional docked or undocked settings
- profile management and saving/loading of shape lists for each telescope
- precise position and parameter control with keyboard shortcuts (`Ctrl` + keys, press the shortcuts button to see the list)
- light, dark and night (red) themes, optional system theme following and compact theme density
- multilanguage (English, Slovenian, German, French)
- multiple platform support (Windows, Linux, macOS; x64 and arm64)
- ASCOM Alpaca camera support: server discovery, live view with exposure, gain and binning controls, FITS frames with Bayer demosaicing
- ASCOM Alpaca telescope mount control: tracking, park/unpark, slew speed and directional movement
- collimation analysis of the live image or of a loaded image file: defocused star (Airy disc) analysis with inside/outside focus, optical tube analysis and guided screw calibration with per-screw adjustment instructions
- automatic check for new versions, application log viewer and about dialog
- background screen capture (e.g. a SharpCap preview) using native OS APIs: GDI on Windows, CoreGraphics on macOS (Screen Recording permission required), X11 on Linux, and the xdg-desktop-portal ScreenCast API with PipeWire on Wayland (asks once to share the screen; requires `gstreamer1.0-tools` and `gstreamer1.0-pipewire`), falling back to `grim` on wlroots desktops such as Raspberry Pi OS and StellarMate (`sudo apt install grim`, no prompt). The camera is offered on these supported platforms; permissions and backend availability are checked when capture starts. On Linux, application windows cannot be excluded from capture and may appear in the captured image.

# Prebuild binaries
Prebuild binary files are available for download on the releases page: https://github.com/sajmons/CollimationCircles/releases/

The releases are self-contained, so you don't need to install .NET. Pick the file that matches your operating system and processor:

| Platform | Architecture | File name |
|----------|--------------|-----------|
| Windows | x64 | `1-CollimationCircles-<version>-win-x64.zip` |
| Windows | arm64 | `2-CollimationCircles-<version>-win-arm64.zip` |
| Linux | x64 | `3-CollimationCircles-<version>-linux-x64.tar.gz` |
| Linux (e.g. Raspberry Pi) | arm64 | `4-CollimationCircles-<version>-linux-arm64.tar.gz` |
| macOS (Intel) | x64 | `5-CollimationCircles-<version>-osx-x64.tar.gz` |
| macOS (Apple Silicon) | arm64 | `6-CollimationCircles-<version>-osx-arm64.tar.gz` |

Windows releases are packaged as ZIP files, while Linux and macOS releases are packaged as tar.gz files which preserve executable permissions.

## Installation on Windows
1. Download the ZIP file for your architecture (win-x64 or win-arm64) from the releases page.
2. Extract the ZIP file to a folder of your choice.
3. Run `CollimationCircles.exe`.

If Windows SmartScreen warns about an unknown publisher, choose **More info** and then **Run anyway**.

## Installation on macOS
Requires macOS 11 or newer. Use the `osx-arm64` file on Apple Silicon and the `osx-x64` file on Intel Macs.

1. Download the tar.gz file from the releases page, either with the browser or from the terminal, for example:
```
cd ~/Downloads
curl -LO <url address from the releases page>
```
2. Extract the archive:
```
tar -xzf <downloaded-file>.tar.gz
```
3. Remove the quarantine attribute, because the app is not notarized by Apple:
```
xattr -dr com.apple.quarantine CollimationCircles.app
```
4. Optionally move the app to the Applications folder:
```
mv CollimationCircles.app /Applications/
```
5. Start the app by double clicking on `CollimationCircles.app` or with this command:
```
open /Applications/CollimationCircles.app
```
6. To use background screen capture, allow the app in **System Settings > Privacy & Security > Screen Recording** and restart the app. To use a camera, allow camera access when macOS asks for it.

## Installation on Linux
Use the `linux-x64` file on regular PCs and the `linux-arm64` file on 64-bit Raspberry Pi OS and other ARM boards.

1. Download the tar.gz file from the releases page: https://github.com/sajmons/CollimationCircles/releases
2. Extract it into a folder:
```
mkdir CollimationCircles
tar -xzf <downloaded-file>.tar.gz -C CollimationCircles
```
3. Run the application by double clicking on `CollimationCircles` in your file manager or with this command:
```
cd CollimationCircles
./CollimationCircles
```
4. Optional packages for background screen capture:
   - X11 session: nothing to install.
   - Wayland session (e.g. Ubuntu): `sudo apt install gstreamer1.0-tools gstreamer1.0-pipewire`. The desktop asks once for permission to share the screen.
   - Wayland with wlroots desktops (e.g. Raspberry Pi OS, StellarMate): `sudo apt install grim`.

If the main window is not transparent, see the known issue below.

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