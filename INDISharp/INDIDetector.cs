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
    public class INDIDetectorBlobEventArgs : EventArgs
	{
		public byte[] Data;
		public string Format;
		public string Name;
        public string Vector;

		public INDIDetectorBlobEventArgs(byte[] data, string name, string vector, string format)
		{
            Vector = vector;
			Name = name;
			Format = format;
			Data = data;
		}
    }
    public class INDIDetectorNumberEventArgs : IsNewNumberEventArgs
    {
        public INDIDetectorNumberType Type;
        public List<INDINumber> Values;
        public INDIDetectorNumberEventArgs(INumberVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "SENSOR_INTEGRATION":
                    Type = INDIDetectorNumberType.Integration;
                    break;
				case "SENSOR_ABORT_INTEGRATION":
					Type = INDIDetectorNumberType.AbortIntegration;
                    break;
                case "SENSOR_TEMPERATURE":
                    Type = INDIDetectorNumberType.Temperature;
                    break;
                case "DETECTOR_SETTINGS":
                    Type = INDIDetectorNumberType.Settings;
                    break;

                case "TIME_LST":
                    Type = INDIDetectorNumberType.TimeLst;
                    break;
                case "GEOGRAPHIC_COORD":
                    Type = INDIDetectorNumberType.GeographicCoord;
                    break;
                case "ATMOSPHERE":
                    Type = INDIDetectorNumberType.Atmosphere;
                    break;
                default:
                    Type = INDIDetectorNumberType.Other;
                    break;
            }
        }
    }
    public class INDIDetectorSwitchEventArgs : IsNewSwitchEventArgs
    {
        public INDIDetectorSwitchType Type;
        public List<INDISwitch> Values;
        public INDIDetectorSwitchEventArgs(ISwitchVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "DETECTOR_COOLER":
                    Type = INDIDetectorSwitchType.Cooler;
                    break;

                case "CONNECTION":
                    Type = INDIDetectorSwitchType.Connection;
                    break;
                case "UPLOAD_MODE":
                    Type = INDIDetectorSwitchType.UploadMode;
                    break;
                default:
                    Type = INDIDetectorSwitchType.Other;
                    break;
            }
        }
    }
    public class INDIDetectorTextEventArgs : IsNewTextEventArgs
    {
        public INDIDetectorTextType Type;
        public List<INDIText> Values;
        public INDIDetectorTextEventArgs(ITextVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "DETECTOR_CFA":
                    Type = INDIDetectorTextType.Cfa;
                    break;

                case "DEVICE_PORT":
                    Type = INDIDetectorTextType.DevicePort;
                    break;
                case "TIME_UTC":
                    Type = INDIDetectorTextType.TimeUtc;
                    break;
                case "UPLOAD_SETTINGS":
                    Type = INDIDetectorTextType.UploadSettings;
                    break;
                case "ACTIVE_DEVICES":
                    Type = INDIDetectorTextType.ActiveDevices;
                    break;
                default:
                    Type = INDIDetectorTextType.Other;
                    break;
            }
        }
    }
    #endregion
    #region Enums
    public enum INDIDetectorNumberType
    {
        TimeLst,
        GeographicCoord,
        Atmosphere,
        Other,

		Temperature,
		Settings,
        Integration,
		AbortIntegration,
		Trigger,
		Resolution,
    }
    public enum INDIDetectorSwitchType
    {
        Connection,
        UploadMode,
        Other,

        Cooler,
        FrameType,
        Compression,
        FrameReset,
    }
    public enum INDIDetectorTextType
    {
        DevicePort,
        TimeUtc,
        UploadSettings,
        ActiveDevices,
        Other,

        Cfa,
    }
    #endregion
    public class INDIDetector : INDIDevice
    {
        public event EventHandler<INDIDetectorBlobEventArgs> IsNewBlob = null;
        public event EventHandler<INDIDetectorNumberEventArgs> IsNewNumber = null;
        public event EventHandler<INDIDetectorSwitchEventArgs> IsNewSwitch = null;
        public event EventHandler<INDIDetectorTextEventArgs> IsNewText = null;
        #region Constructors / Initialization
        public INDIDetector (string name, INDIClient host, bool client = true)
			: base (name, host, client)
		{
			EnableBLOB (true);
			if (!client) {
				AddNumberVector (new INumberVector (Name, "DETECTOR_CAPTURE", "Capture", "Main Control", "rw", "", new List<INDINumber> {
					new INDINumber ("DETECTOR_CAPTURE_VALUE", "Duration (s)", "%5.2f", 0.05, 10000.0, 0.05, 1.0)
				}));
				AddSwitchVector (new ISwitchVector (Name, "DETECTOR_ABORT_CAPTURE", "Expose Abort", "Main Control", "rw", "AtMostOne", new List<INDISwitch> {
					new INDISwitch ("ABORT", "Abort", false)
				}));
				AddNumberVector (new INumberVector (Name, "DETECTOR_TEMPERATURE", "Temperature", "Main Control", "rw", "", new List<INDINumber> {
					new INDINumber ("DETECTOR_TEMPERATURE_VALUE", "Temperature (C)", "%5.2f", -50.0, 50.0, 0.0, 20.0)
				}));
				AddNumberVector (new INumberVector (Name, "DETECTOR_INFO", "Detector Information", "Image Info", "ro", "", new List<INDINumber> {
					new INDINumber ("DETECTOR_SAMPLERATE", "Bandwidth (Hz)", "%18.2f", 0.01, 1.0e+15, 0.01, 1.0e+6),
					new INDINumber ("DETECTOR_FREQUENCY", "Observed frequency (Hz)", "%18.2f", 0.01, 1.0e+15, 0.01, 1.42e+9),
					new INDINumber ("DETECTOR_BITSPERSAMPLE", "Bits per sample", "%3.0f", 1, 64, 1, 8)
				}));
				AddBlobVector (new IBlobVector (Name, "DETECTOR", "Capture", "Data Streams", "ro", "", new List<INDIBlob> {
					new INDIBlob ("CONTINUUM", Name + " continuum data", ".fits", new byte[1], 1),
					new INDIBlob ("SPECTRUM", Name + " spectrum data", ".fits", new byte[1], 1),
				}));
                DriverInterface |= DRIVER_INTERFACE.DETECTOR_INTERFACE;
            }
		}
        #endregion

        #region Standard Methods
        public void StartCapture(Double duration)
        {
            try
            {
                SetNumber("DETECTOR_CAPTURE", "DETECTOR_CAPTURE_VALUE", duration);
            }
            catch
            {
            }
        }

        public void Abort()
        {
            try
            {
                SetSwitch("DETECTOR_ABORT_CAPTURE", "ABORT", true);
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
                    IsNewNumber?.Invoke(this, new INDIDetectorNumberEventArgs(e.Vector, e.Device));
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
                    IsNewSwitch?.Invoke(this, new INDIDetectorSwitchEventArgs(e.Vector, e.Device));
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
                    IsNewText?.Invoke(this, new INDIDetectorTextEventArgs(e.Vector, e.Device));
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
                        IsNewBlob?.Invoke(this, new INDIDetectorBlobEventArgs(e.Vector.Values[i].value, e.Vector.Values[i].Name, e.Vector.Name, e.Vector.Values[0].format));
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
        public Double CaptureTime
        {
            get
            {
                try
                {
                    return GetNumber("DETECTOR_CAPTURE", "DETECTOR_CAPTURE_VALUE").value;
                }
                catch
                {
                }
                return 0.0;
            }
		}

		public double SampleRate
		{
			get
			{
				try
				{
					return GetNumber("DETECTOR_SETTINGS", "DETECTOR_SAMPLERATE").value;
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
					SetNumber("DETECTOR_SETTINGS", "DETECTOR_SAMPLERATE", value);
				}
				catch { }
			}
		}

		public double CenterFrequency
		{
			get
			{
				try
				{
					return GetNumber("DETECTOR_SETTINGS", "DETECTOR_FREQUENCY").value;
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
					SetNumber("DETECTOR_SETTINGS", "DETECTOR_FREQUENCY", value);
				}
				catch { }
			}
		}

		public double Bps
		{
			get
			{
				try
				{
					return GetNumber("DETECTOR_INFO", "DETECTOR_BITSPERSAMPLE").value;
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
					SetNumber("DETECTOR_INFO", "DETECTOR_BITSPERSAMPLE", value);
				}
				catch { }
			}
		}

        public Boolean CoolerStarted
        {
            get
            {
                try
                {
                    return GetSwitch("DETECTOR_COOLER", "COOLER_ON").value;
                }
                catch { }
                return false;
            }
            set
            {
                try
                {
                    SetSwitchVector("DETECTOR_COOLER", value ? 0 : 1);
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
                    return GetNumber("DETECTOR_COOLER_POWER", "DETECTOR_COOLER_VALUE").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DETECTOR_COOLER_POWER", "DETECTOR_COOLER_VALUE", value);
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
                    return GetNumberVector("DETECTOR_COOLER_POWER").Permission == "rw";
                }
                catch { }
                return false;
            }
        }

        public Double MinCapture
        {
            get
            {
                try
                {
                    return GetNumber("DETECTOR_CAPTURE", "DETECTOR_CAPTURE_VALUE").min;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double MaxCapture
        {
            get
            {
                try
                {
                    return GetNumber("DETECTOR_CAPTURE", "DETECTOR_CAPTURE_VALUE").max;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double CaptureStep
        {
            get
            {
                try
                {
                    return GetNumber("DETECTOR_CAPTURE", "DETECTOR_CAPTURE_VALUE").step;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double DetectorTemperature
        {
            get
            {
                try
                {
                    return GetNumber("DETECTOR_TEMPERATURE", "DETECTOR_TEMPERATURE_VALUE").value;
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
                    if (CanSetDetectorTemperature)
                    {
                        SetNumber("DETECTOR_TEMPERATURE", "DETECTOR_TEMPERATURE_VALUE", value);
                    }
                }
                catch
                {
                }
            }
        }

        public Boolean CanSetDetectorTemperature
        {
            get
            {
                try
                {
                    return (GetNumberVector("DETECTOR_TEMPERATURE").Permission == "rw");
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

