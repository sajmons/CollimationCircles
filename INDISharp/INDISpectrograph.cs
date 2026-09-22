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
	public class INDISpectrographBlobEventArgs : EventArgs
	{
		public byte[] Data;
		public string Format;
		public string Name;
        public string Vector;

		public INDISpectrographBlobEventArgs(byte[] data, string name, string vector, string format)
		{
            Vector = vector;
			Name = name;
			Format = format;
			Data = data;
		}
    }
    public class INDISpectrographNumberEventArgs : IsNewNumberEventArgs
    {
        public INDISpectrographNumberType Type;
        public List<INDINumber> Values;
        public INDISpectrographNumberEventArgs(INumberVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
			case "INTEGRATION":
                    Type = INDISpectrographNumberType.Integration;
                    break;
                case "SENSOR_ABORT_INTEGRATION":
				Type = INDISpectrographNumberType.AbortIntegration;
                    break;
                case "SENSOR_TEMPERATURE":
                    Type = INDISpectrographNumberType.Temperature;
                    break;
                case "SPECTROGRAPH_SETTINGS":
                    Type = INDISpectrographNumberType.Settings;
                    break;

                case "TIME_LST":
                    Type = INDISpectrographNumberType.TimeLst;
                    break;
                case "GEOGRAPHIC_COORD":
                    Type = INDISpectrographNumberType.GeographicCoord;
                    break;
                case "ATMOSPHERE":
                    Type = INDISpectrographNumberType.Atmosphere;
                    break;
                default:
                    Type = INDISpectrographNumberType.Other;
                    break;
            }
		}
    }
    public class INDISpectrographSwitchEventArgs : IsNewSwitchEventArgs
    {
        public INDISpectrographSwitchType Type;
        public List<INDISwitch> Values;
        public INDISpectrographSwitchEventArgs(ISwitchVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "SPECTROGRAPH_COOLER":
                    Type = INDISpectrographSwitchType.Cooler;
                    break;

                case "CONNECTION":
                    Type = INDISpectrographSwitchType.Connection;
                    break;
                case "UPLOAD_MODE":
                    Type = INDISpectrographSwitchType.UploadMode;
                    break;
                default:
                    Type = INDISpectrographSwitchType.Other;
                    break;
            }
        }
    }
    public class INDISpectrographTextEventArgs : IsNewTextEventArgs
    {
        public INDISpectrographTextType Type;
        public List<INDIText> Values;
        public INDISpectrographTextEventArgs(ITextVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "SPECTROGRAPH_CFA":
                    Type = INDISpectrographTextType.Cfa;
                    break;

                case "DEVICE_PORT":
                    Type = INDISpectrographTextType.DevicePort;
                    break;
                case "TIME_UTC":
                    Type = INDISpectrographTextType.TimeUtc;
                    break;
                case "UPLOAD_SETTINGS":
                    Type = INDISpectrographTextType.UploadSettings;
                    break;
                case "ACTIVE_DEVICES":
                    Type = INDISpectrographTextType.ActiveDevices;
                    break;
                default:
                    Type = INDISpectrographTextType.Other;
                    break;
            }
        }
    }
    #endregion
    #region Enums
    public enum INDISpectrographNumberType
    {
        TimeLst,
        GeographicCoord,
        Atmosphere,
        Other,

		Temperature,
		Integration,
		AbortIntegration,
        Samplerate,
		Frequency,
		BPS,
		Bandwidth,
		Gain,
		Antenna,
		Settings,
    }
    public enum INDISpectrographSwitchType
    {
        Connection,
        UploadMode,
        Other,

        Cooler,
        FrameType,
        Compression,
        FrameReset,
    }
    public enum INDISpectrographTextType
    {
        DevicePort,
        TimeUtc,
        UploadSettings,
        ActiveDevices,
        Other,

        Cfa,
    }
    #endregion
    public class INDISpectrograph : INDIDevice
    {
        public event EventHandler<INDISpectrographBlobEventArgs> IsNewBlob = null;
        public event EventHandler<INDISpectrographNumberEventArgs> IsNewNumber = null;
        public event EventHandler<INDISpectrographSwitchEventArgs> IsNewSwitch = null;
        public event EventHandler<INDISpectrographTextEventArgs> IsNewText = null;
        #region Constructors / Initialization
        public INDISpectrograph (string name, INDIClient host, bool client = true)
			: base (name, host, client)
		{
			EnableBLOB (true);
			if (!client) {
				AddNumberVector (new INumberVector (Name, "SENSOR_INTEGRATION", "Capture", "Main Control", "rw", "", new List<INDINumber> {
					new INDINumber ("SENSOR_INTEGRATION_VALUE", "Duration (s)", "%5.2f", 0.05, 10000.0, 0.05, 1.0)
				}));
				AddSwitchVector (new ISwitchVector (Name, "SENSOR_ABORT_INTEGRATION", "Expose Abort", "Main Control", "rw", "AtMostOne", new List<INDISwitch> {
					new INDISwitch ("ABORT", "Abort", false)
				}));
				AddNumberVector (new INumberVector (Name, "SENSOR_TEMPERATURE", "Temperature", "Main Control", "rw", "", new List<INDINumber> {
					new INDINumber ("SENSOR_TEMPERATURE_VALUE", "Temperature (C)", "%5.2f", -50.0, 50.0, 0.0, 20.0)
				}));
				AddNumberVector (new INumberVector (Name, "SPECTROGRAPH_INFO", "Spectrograph Information", "Image Info", "ro", "", new List<INDINumber> {
					new INDINumber ("SPECTROGRAPH_SAMPLERATE", "Bandwidth (Hz)", "%18.2f", 0.01, 1.0e+15, 0.01, 1.0e+6),
					new INDINumber ("SPECTROGRAPH_FREQUENCY", "Observed frequency (Hz)", "%18.2f", 0.01, 1.0e+15, 0.01, 1.42e+9),
					new INDINumber ("SPECTROGRAPH_BITSPERSAMPLE", "Bits per sample", "%3.0f", 1, 64, 1, 8)
				}));
				AddBlobVector (new IBlobVector (Name, "SPECTROGRAPH", "Capture", "Data Streams", "ro", "", new List<INDIBlob> {
					new INDIBlob ("CONTINUUM", Name + " continuum data", ".fits", new byte[1], 1),
					new INDIBlob ("SPECTRUM", Name + " spectrum data", ".fits", new byte[1], 1),
				}));
                DriverInterface |= DRIVER_INTERFACE.SPECTROGRAPH_INTERFACE;
			}
		}
        #endregion

        #region Standard Methods
        public void StartCapture(Double duration)
        {
            try
            {
                SetNumber("SENSOR_INTEGRATION", "SENSOR_INTEGRATION_VALUE", duration);
            }
            catch
            {
            }
        }

        public void Abort()
        {
            try
            {
                SetSwitch("SENSOR_ABORT_INTEGRATION", "ABORT", true);
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
                    IsNewNumber?.Invoke(this, new INDISpectrographNumberEventArgs(e.Vector, e.Device));
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
                    IsNewSwitch?.Invoke(this, new INDISpectrographSwitchEventArgs(e.Vector, e.Device));
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
                    IsNewText?.Invoke(this, new INDISpectrographTextEventArgs(e.Vector, e.Device));
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
                        IsNewBlob?.Invoke(this, new INDISpectrographBlobEventArgs(e.Vector.Values[i].value, e.Vector.Values[i].Name, e.Vector.Name, e.Vector.Values[0].format));
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
                    return GetNumber("SENSOR_INTEGRATION", "SENSOR_INTEGRATION_VALUE").value;
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
					return GetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_SAMPLERATE").value;
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
					SetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_SAMPLERATE", value);
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
					return GetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_FREQUENCY").value;
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
					SetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_FREQUENCY", value);
				}
				catch { }
			}
		}

		public double Gain
		{
			get
			{
				try
				{
					return GetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_GAIN").value;
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
					SetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_GAIN", value);
				}
				catch { }
			}
		}

		public double Bandwidth
		{
			get
			{
				try
				{
					return GetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_BANDWIDTH").value;
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
					SetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_BANDWIDTH", value);
				}
				catch { }
			}
		}

		public double Samplerate
		{
			get
			{
				try
				{
					return GetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_SAMPLERATE").value;
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
					SetNumber("SPECTROGRAPH_SETTINGS", "SPECTROGRAPH_SAMPLERATE", value);
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
                    return GetSwitch("SPECTROGRAPH_COOLER", "COOLER_ON").value;
                }
                catch { }
                return false;
            }
            set
            {
                try
                {
                    SetSwitchVector("SPECTROGRAPH_COOLER", value ? 0 : 1);
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
                    return GetNumber("SPECTROGRAPH_COOLER_POWER", "SPECTROGRAPH_COOLER_VALUE").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("SPECTROGRAPH_COOLER_POWER", "SPECTROGRAPH_COOLER_VALUE", value);
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
                    return GetNumberVector("SPECTROGRAPH_COOLER_POWER").Permission == "rw";
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
                    return GetNumber("SENSOR_INTEGRATION", "SENSOR_INTEGRATION_VALUE").min;
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
                    return GetNumber("SENSOR_INTEGRATION", "SENSOR_INTEGRATION_VALUE").max;
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
                    return GetNumber("SENSOR_INTEGRATION", "SENSOR_INTEGRATION_VALUE").step;
                }
                catch
                {
                }
                return 0.0;
            }
        }

        public Double SpectrographTemperature
        {
            get
            {
                try
                {
                    return GetNumber("SENSOR_TEMPERATURE", "SENSOR_TEMPERATURE_VALUE").value;
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
                    if (CanSetSpectrographTemperature)
                    {
                        SetNumber("SENSOR_TEMPERATURE", "SENSOR_TEMPERATURE_VALUE", value);
                    }
                }
                catch
                {
                }
            }
        }

        public Boolean CanSetSpectrographTemperature
        {
            get
            {
                try
                {
                    return (GetNumberVector("SENSOR_TEMPERATURE").Permission == "rw");
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

