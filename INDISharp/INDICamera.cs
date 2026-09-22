/*
 MIT License

 Copyright (c) 2015-2021 Ilia Platone

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace INDI
{
    #region Custom Event Argument classes
    public class INDICameraBlobEventArgs : EventArgs
	{
		public byte[] ImageData;
		public string Format;
		public string Name;
        public string Vector;

		public INDICameraBlobEventArgs(byte[] imagedata, string name, string vector, string format)
		{
            Vector = vector;
			Name = name;
			Format = format;
			ImageData = imagedata;
		}
    }
    public class INDICameraNumberEventArgs : IsNewNumberEventArgs
    {
        public INDICameraNumberType Type;
        public List<INDINumber> Values;
        public INDICameraNumberEventArgs(INumberVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "CCD_EXPOSURE":
                    Type = INDICameraNumberType.Exposure;
                    break;
                case "CCD_ABORT_EXPOSURE":
                    Type = INDICameraNumberType.AbortExposure;
                    break;
                case "CCD_FRAME":
                    Type = INDICameraNumberType.FrameSize;
                    break;
                case "CCD_TEMPERATURE":
                    Type = INDICameraNumberType.Temperature;
                    break;
                case "CCD_COOLER_POWER":
                    Type = INDICameraNumberType.CoolerPower;
                    break;
                case "CCD_BINNING":
                    Type = INDICameraNumberType.Binning;
                    break;
                case "CCD_INFO":
                    Type = INDICameraNumberType.Informations;
                    break;

                case "TIME_LST":
                    Type = INDICameraNumberType.TimeLst;
                    break;
                case "GEOGRAPHIC_COORD":
                    Type = INDICameraNumberType.GeographicCoord;
                    break;
                case "ATMOSPHERE":
                    Type = INDICameraNumberType.Atmosphere;
                    break;
                default:
                    Type = INDICameraNumberType.Other;
                    break;
            }
        }
    }
    public class INDICameraSwitchEventArgs : IsNewSwitchEventArgs
    {
        public INDICameraSwitchType Type;
        public List<INDISwitch> Values;
        public INDICameraSwitchEventArgs(ISwitchVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "CCD_COOLER":
                    Type = INDICameraSwitchType.Cooler;
                    break;
                case "CCD_FRAME_TYPE":
                    Type = INDICameraSwitchType.FrameType;
                    break;
                case "CCD_COMPRESSION":
                    Type = INDICameraSwitchType.Compression;
                    break;
                case "CCD_FRAME_RESET":
                    Type = INDICameraSwitchType.FrameReset;
                    break;

                case "CONNECTION":
                    Type = INDICameraSwitchType.Connection;
                    break;
                case "UPLOAD_MODE":
                    Type = INDICameraSwitchType.UploadMode;
                    break;
                default:
                    Type = INDICameraSwitchType.Other;
                    break;
            }
        }
    }
    public class INDICameraTextEventArgs : IsNewTextEventArgs
    {
        public INDICameraTextType Type;
        public List<INDIText> Values;
        public INDICameraTextEventArgs(ITextVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "CCD_CFA":
                    Type = INDICameraTextType.Cfa;
                    break;

                case "DEVICE_PORT":
                    Type = INDICameraTextType.DevicePort;
                    break;
                case "TIME_UTC":
                    Type = INDICameraTextType.TimeUtc;
                    break;
                case "UPLOAD_SETTINGS":
                    Type = INDICameraTextType.UploadSettings;
                    break;
                case "ACTIVE_DEVICES":
                    Type = INDICameraTextType.ActiveDevices;
                    break;
                default:
                    Type = INDICameraTextType.Other;
                    break;
            }
        }
    }
    #endregion
    #region Enums
    public enum INDICameraNumberType
    {
        TimeLst,
        GeographicCoord,
        Atmosphere,
        Other,

        Exposure,
        AbortExposure,
        FrameSize,
        Temperature,
        CoolerPower,
        Binning,
        Informations,
    }
    public enum INDICameraSwitchType
    {
        Connection,
        UploadMode,
        Other,

        Cooler,
        FrameType,
        Compression,
        FrameReset,
    }
    public enum INDICameraTextType
    {
        DevicePort,
        TimeUtc,
        UploadSettings,
        ActiveDevices,
        Other,

        Cfa,
    }
    public enum INDIFrameType
    {
        LIGHT = 0,
        BIAS,
        DARK,
        FLAT
    };
    #endregion
    public class INDICamera : INDIDevice
	{
		public event EventHandler<INDICameraBlobEventArgs> IsNewBlob = null;
        public event EventHandler<INDICameraNumberEventArgs> IsNewNumber = null;
        public event EventHandler<INDICameraSwitchEventArgs> IsNewSwitch = null;
        public event EventHandler<INDICameraTextEventArgs> IsNewText = null;
        #region Constructors / Initialization
        public INDICamera(string name, INDIClient host, bool client = true)
            : base(name, host, client)
        {
            EnableBLOB(true);
            if (!client)
            {
                AddNumberVector(new INumberVector(Name, "CCD_EXPOSURE", "Exposure", "Main Control", "rw", "", new List<INDINumber>
            {
                new INDINumber("CCD_EXPOSURE_VALUE", "Duration (s)", "%5.2f", 0.05, 10000.0, 0.05, 1.0)
            }));
                AddSwitchVector(new ISwitchVector(Name, "CCD_ABORT_EXPOSURE", "Expose Abort", "Main Control", "rw", "AtMostOne", new List<INDISwitch>
            {
                new INDISwitch("ABORT", "Abort", false)
            }));
                AddNumberVector(new INumberVector(Name, "CCD_TEMPERATURE", "Temperature", "Main Control", "rw", "", new List<INDINumber>
            {
                new INDINumber("CCD_TEMPERATURE_VALUE", "Temperature (C)", "%5.2f", -50.0, 50.0, 0.0, 20.0)
            }));
                AddNumberVector(new INumberVector(Name, "CCD_FRAME", "Frame", "Image Settings", "rw", "", new List<INDINumber>
            {
                new INDINumber("X", "Left", "%4.0f", 0.0, 16000.0, 1.0, 0.0),
                new INDINumber("Y", "Top", "%4.0f", 0.0, 16000.0, 1.0, 0.0),
                new INDINumber("WIDTH", "Width", "%4.0f", 0.0, 16000.0, 1.0, 16000.0),
                new INDINumber("HEIGHT", "Height", "%4.0f", 0.0, 16000.0, 1.0, 16000.0)
            }));
                AddNumberVector(new INumberVector(Name, "CCD_BINNING", "Binning", "Image Settings", "rw", "", new List<INDINumber>
            {
                new INDINumber("HOR_BIN", "X", "%2.0f", 1.0, 4.0, 1.0, 1.0),
                new INDINumber("VER_BIN", "Y", "%2.0f", 1.0, 4.0, 1.0, 1.0)
            }));
                AddSwitchVector(new ISwitchVector(Name, "CCD_COMPRESSION", "Image", "Image Settings", "rw", "OneOfMany", new List<INDISwitch>
            {
                new INDISwitch("CCD_COMPRESS", "Compress", false),
                new INDISwitch("CCD_RAW", "Raw", true)
            }));
                AddSwitchVector(new ISwitchVector(Name, "CCD_FRAME_TYPE", "Frame Type", "Image Settings", "rw", "OneOfMany", new List<INDISwitch>
            {
                new INDISwitch("FRAME_LIGHT", "Light", true),
                new INDISwitch("FRAME_BIAS", "Bias", false),
                new INDISwitch("FRAME_DARK", "Dark", false),
                new INDISwitch("FRAME_FLAT", "Flat", false)
            }));
                AddNumberVector(new INumberVector(Name, "CCD_INFO", "CCD Information", "Image Info", "ro", "", new List<INDINumber>
            {
                new INDINumber("CCD_MAX_X", "Resolution x", "%4.0f", 1.0, 16000.0, 0.0, 16000.0),
                new INDINumber("CCD_MAX_Y", "Resolution y", "%4.0f", 1.0, 16000.0, 0.0, 16000.0),
                new INDINumber("CCD_PIXEL_SIZE", "Pixel size (um)", "%5.2f", 1.0, 40.0, 0.0, 9.0),
                new INDINumber("CCD_PIXEL_SIZE_X", "Pixel size X", "%5.2f", 1.0, 40.0, 0.0, 9.0),
                new INDINumber("CCD_PIXEL_SIZE_Y", "Pixel size Y", "%5.2f", 1.0, 40.0, 0.0, 9.0),
                new INDINumber("CCD_BITSPERPIXEL", "Bits per pixel", "%3.0f", 8.0, 64.0, 0.0, 16.0)
            }));
                AddTextVector(new ITextVector(Name, "CCD_CFA", "Bayer Info", "Image Info", "ro", "", new List<INDIText>
            {
                new INDIText("CFA_OFFSET_X", "X Offset", "0"),
                new INDIText("CFA_OFFSET_Y", "Y Offset", "0"),
                new INDIText("CFA_TYPE", "Filter", "")
            }));
                AddBlobVector(new IBlobVector(Name, "CCD", "Image Data", "Image Streams", "ro", "", new List<INDIBlob>
                {
                    new INDIBlob("CCD1", Name + " image data", ".fits", new byte[1], 1),
                }));
                DriverInterface |= DRIVER_INTERFACE.CCD_INTERFACE;
            }
        }
        #endregion

        #region Standard Methods
        public void StartExposure(Double duration, INDIFrameType type)
        {
            try
            {
                SetSwitchVector("CCD_FRAME_TYPE", (Int32)type);
                SetNumber("CCD_EXPOSURE", "CCD_EXPOSURE_VALUE", duration);
            }
            catch
            {
            }
        }

        public void ResetFrame()
        {
            try
            {
                SetSwitch("CCD_FRAME_RESET", "RESET", true);
            }
            catch { }
        }

        public void Abort()
        {
            try
            {
                SetSwitch("CCD_ABORT_EXPOSURE", "ABORT", true);
            }
            catch { }
        }

        public override void isNewNumber(Object sender, IsNewNumberEventArgs e)
        {
            base.isNewNumber(sender, e);
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            try
            {
                if (e.Vector.Device == Name)
                {
                    IsNewNumber?.Invoke(this, new INDICameraNumberEventArgs(e.Vector, e.Device));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public override void isNewSwitch(Object sender, IsNewSwitchEventArgs e)
        {
            base.isNewSwitch(sender, e);
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            try
            {
                if (e.Vector.Device == Name)
                {
                    IsNewSwitch?.Invoke(this, new INDICameraSwitchEventArgs(e.Vector, e.Device));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public override void isNewText(Object sender, IsNewTextEventArgs e)
        {
            base.isNewText(sender, e);
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            try
            {
                if (e.Vector.Device == Name)
                {
                    IsNewText?.Invoke(this, new INDICameraTextEventArgs(e.Vector, e.Device));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public override void isNewBlob(Object sender, IsNewBlobEventArgs e)
        {
            base.isNewBlob(sender, e);
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            try
            {
                if (e.Vector.Device == Name)
                {
                    INDIClient caller = (INDIClient)sender;
                    for (int i = 0; i < e.Vector.Values.Count; i++)
                    {
                        Console.WriteLine("Received BLOB " + e.Vector.Values[i].Name + " of size " + e.Vector.Values[i].size + " from device " + e.Device + "@" + caller.Address + ":" + caller.Port);
                        IsNewBlob?.Invoke(this, new INDICameraBlobEventArgs(e.Vector.Values[i].value, e.Vector.Values[i].Name, e.Vector.Name, e.Vector.Values[0].format));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        #endregion

        #region Standard Properties
        public Boolean CompessionEnabled
        {
            set
            {
                try
                {
                    SetSwitch("CCD_COMPRESSION", "CCD_COMPRESSION_ON", value);
                }
                catch
                {
                }
            }
            get
            {
                try
                {
                    return GetSwitch("CCD_COMPRESSION", "CCD_COMPRESSION_ON").value;
                }
                catch
                {
                }
                return false;
            }
        }

        public Int16 BayerX
        {
            get
            {
                try
                {
                    Int16.Parse(GetText("CCD_CFA", "CFA_OFFSET_X").value);
                }
                catch
                {
                }
                return 0;
            }
        }

        public Int16 BayerY
        {
            get
            {
                try
                {
                    Int16.Parse(GetText("CCD_CFA", "CFA_OFFSET_Y").value);
                }
                catch
                {
                }
                return 0;
            }
        }

        public Double ExposureTime
        {
            get
            {
                try
                {
                    return GetNumber("CCD_EXPOSURE", "CCD_EXPOSURE_VALUE").value;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Boolean CoolerStarted
        {
            get
            {
                try
                {
                    return GetSwitch("CCD_COOLER", "COOLER_ON").value;
                }
                catch { }
                return false;
            }
            set
            {
                try
                {
                    SetSwitchVector("CCD_COOLER", value ? 0 : 1);
                }
                catch { }
            }
        }

        public Double CoolerPower
        {
            get
            {
                try
                {
                    return GetNumber("CCD_COOLER_POWER", "CCD_COOLER_VALUE").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("CCD_COOLER_POWER", "CCD_COOLER_VALUE", value);
                }
                catch { }
            }
        }

        public Boolean CanSetCoolerPower
        {
            get
            {
                try
                {
                    return GetNumberVector("CCD_COOLER_POWER").Permission == "rw";
                }
                catch { }
                return false;
            }
        }

        public Int16 BinX
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_BINNING", "HOR_BIN").value;
                }
                catch
                {
                }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("CCD_BINNING", "HOR_BIN", value);
                }
                catch
                {
                }
            }
        }

        public Int16 BinY
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_BINNING", "VER_BIN").value;
                }
                catch
                {
                }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("CCD_BINNING", "VER_BIN", value);
                }
                catch
                {
                }
            }
        }

        public Int16 MaxBinX
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_BINNING", "HOR_BIN").max;
                }
                catch
                {
                }
                return 0;
            }
        }

        public Int16 MaxBinY
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_BINNING", "VER_BIN").max;
                }
                catch
                {
                }
                return 0;
            }
        }

        public Int32 StartX
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_FRAME", "X").value;
                }
                catch
                {
                }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("CCD_FRAME", "X", value);
                }
                catch
                {
                }
            }
        }

        public Int32 StartY
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_FRAME", "Y").value;
                }
                catch
                {
                }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("CCD_FRAME", "Y", value);
                }
                catch
                {
                }
            }
        }

        public Int32 Width
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_FRAME", "WIDTH").value;
                }
                catch
                {
                }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("CCD_FRAME", "WIDTH", value);
                }
                catch
                {
                }
            }
        }

        public Int32 Height
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_FRAME", "HEIGHT").value;
                }
                catch
                {
                }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("CCD_FRAME", "HEIGHT", value);
                }
                catch
                {
                }
            }
        }

        public Int32 FullWidth
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_INFO", "CCD_MAX_X").value;
                }
                catch
                {
                }
                return 0;
            }
        }

        public Int32 FullHeight
        {
            get
            {
                try
                {
                    return (Int16)GetNumber("CCD_INFO", "CCD_MAX_Y").value;
                }
                catch
                {
                }
                return 0;
            }
        }

        public Double MinExposure
        {
            get
            {
                try
                {
                    return GetNumber("CCD_EXPOSURE", "CCD_EXPOSURE_VALUE").min;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double MaxExposure
        {
            get
            {
                try
                {
                    return GetNumber("CCD_EXPOSURE", "CCD_EXPOSURE_VALUE").max;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double ExposureStep
        {
            get
            {
                try
                {
                    return GetNumber("CCD_EXPOSURE", "CCD_EXPOSURE_VALUE").step;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double MaxADU
        {
            get
            {
                try
                {
                    return 65536;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double PixelSizeX
        {
            get
            {
                try
                {
                    return GetNumber("CCD_INFO", "CCD_PIXEL_SIZE_X").value;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double PixelSizeY
        {
            get
            {
                try
                {
                    return GetNumber("CCD_INFO", "CCD_PIXEL_SIZE_Y").value;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double CCDTemperature
        {
            get
            {
                try
                {
                    return GetNumber("CCD_TEMPERATURE", "CCD_TEMPERATURE_VALUE").value;
                }
                catch
                {
                }
                return 0.0;
            }
            set
            {
                try
                {
                    if (CanSetCCDTemperature)
                    {
                        SetNumber("CCD_TEMPERATURE", "CCD_TEMPERATURE_VALUE", value);
                    }
                }
                catch
                {
                }
            }
        }

        public Boolean CanSetCCDTemperature
        {
            get
            {
                try
                {
                    return (GetNumberVector("CCD_TEMPERATURE").Permission == "rw");
                }
                catch
                {
                }
                return false;
            }
        }
        #endregion
    }
}

