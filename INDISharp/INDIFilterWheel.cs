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
    public class INDIFilterWheelNumberEventArgs : IsNewNumberEventArgs
    {
        public INDIFilterWheelNumberType Type;
        public List<INDINumber> Values;
        public INDIFilterWheelNumberEventArgs(INumberVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "FILTER_SLOT":
                    Type = INDIFilterWheelNumberType.Slot;
                    break;

                case "TIME_LST":
                    Type = INDIFilterWheelNumberType.TimeLst;
                    break;
                case "GEOGRAPHIC_COORD":
                    Type = INDIFilterWheelNumberType.GeographicCoord;
                    break;
                case "ATMOSPHERE":
                    Type = INDIFilterWheelNumberType.Atmosphere;
                    break;
                default:
                    Type = INDIFilterWheelNumberType.Other;
                    break;
            }
        }
    }
    public class INDIFilterWheelSwitchEventArgs : IsNewSwitchEventArgs
    {
        public INDIFilterWheelSwitchType Type;
        public List<INDISwitch> Values;
        public INDIFilterWheelSwitchEventArgs(ISwitchVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {

                case "CONNECTION":
                    Type = INDIFilterWheelSwitchType.Connection;
                    break;
                case "UPLOAD_MODE":
                    Type = INDIFilterWheelSwitchType.UploadMode;
                    break;
                default:
                    Type = INDIFilterWheelSwitchType.Other;
                    break;
            }
        }
    }
    public class INDIFilterWheelTextEventArgs : IsNewTextEventArgs
    {
        public INDIFilterWheelTextType Type;
        public List<INDIText> Values;
        public INDIFilterWheelTextEventArgs(ITextVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "FILTER_NAME":
                    Type = INDIFilterWheelTextType.Name;
                    break;

                case "DEVICE_PORT":
                    Type = INDIFilterWheelTextType.DevicePort;
                    break;
                case "TIME_UTC":
                    Type = INDIFilterWheelTextType.TimeUtc;
                    break;
                case "UPLOAD_SETTINGS":
                    Type = INDIFilterWheelTextType.UploadSettings;
                    break;
                case "ACTIVE_DEVICES":
                    Type = INDIFilterWheelTextType.ActiveDevices;
                    break;
                default:
                    Type = INDIFilterWheelTextType.Other;
                    break;
            }
        }
    }
    #endregion
    #region Enums
    public enum INDIFilterWheelNumberType
    {
        TimeLst,
        GeographicCoord,
        Atmosphere,
        Other,

        Slot,
    }
    public enum INDIFilterWheelSwitchType
    {
        Connection,
        UploadMode,
        Other,
    }
    public enum INDIFilterWheelTextType
    {
        DevicePort,
        TimeUtc,
        UploadSettings,
        ActiveDevices,
        Other,

        Name,
    }
    #endregion
    public class INDIFilterWheel : INDIDevice
    {
        public event EventHandler<INDIFilterWheelNumberEventArgs> IsNewNumber = null;
        public event EventHandler<INDIFilterWheelSwitchEventArgs> IsNewSwitch = null;
        public event EventHandler<INDIFilterWheelTextEventArgs> IsNewText = null;
        #region Constructors / Initialization
        public INDIFilterWheel(string name, INDIClient host, bool client = true)
            : base(name, host, client)
        {
            if (!client)
            {
                AddNumberVector(new INumberVector(Name, "FILTER_SLOT", "The filter wheel's current slot number", "Main Control", "rw", "", new List<INDINumber>
            {
                new INDINumber("FILTER_SLOT_VALUE", "Slot number", "%2.0f", 0.00, 99.0, 1.0, 0.0)
            }));
                AddTextVector(new ITextVector(Name, "FILTER_NAME_VALUE", "The filter wheel's current slot name", "Main Control", "ro", "", new List<INDIText>
            {
                new INDIText("FILTER_NAME", "Slot name", "LIGHT")
            }));
                DriverInterface |= DRIVER_INTERFACE.FILTER_INTERFACE;
            }
        }
        #endregion

        #region Standard Methods
        public override void isNewNumber(Object sender, IsNewNumberEventArgs e)
        {
            base.isNewNumber(sender, e);
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            try
            {
                if (e.Vector.Device == Name)
                {
                    IsNewNumber?.Invoke(this, new INDIFilterWheelNumberEventArgs(e.Vector, e.Device));
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
                    IsNewSwitch?.Invoke(this, new INDIFilterWheelSwitchEventArgs(e.Vector, e.Device));
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
                    IsNewText?.Invoke(this, new INDIFilterWheelTextEventArgs(e.Vector, e.Device));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        #endregion
		#region Standard Properties
		public Int32 FilterSlots
		{
			get
			{
				try
				{
					return FilterNames.Length;
				}
				catch
				{
					return 0;
				}
			}
		}

        public Int32 FilterSlot
        {
            get
            {
                try
                {
                    return (Int32)GetNumber("FILTER_SLOT", "FILTER_SLOT_VALUE").value;
                }
                catch
                {
                    return 0;
                }
            }
            set
            {
                try
                {
                    Int32 v = value;
                    if (v < 1)
                        v = 1;
                    SetNumber("FILTER_SLOT", "FILTER_SLOT_VALUE", v);
                }
                catch
                {
                }
            }
		}

		public string[] FilterNames
		{
			get
			{
				try
				{
					List<string> ret = new List<string>();
					foreach(INDIText text in GetTextVector("FILTER_NAME").Values)
						ret.Add(text.value);
					return ret.ToArray();
				}
				catch
				{
					throw new ArgumentException();
				}
			}
			set
			{
				try
				{
					int i = 1;
					foreach (string val in value)
					{
						SetText("FILTER_NAME", "FILTER_SLOT_NAME_" + i++, val);
					}
				}
				catch
				{
					throw new ArgumentException();
				}
			}
		}

		public string FilterName
		{
			get
			{
				try
				{
					return GetText("FILTER_NAME", "FILTER_SLOT_NAME_" + FilterSlot).value;
				}
				catch
				{
					throw new ArgumentException();
				}
			}
			set
			{
				try
				{
					SetText("FILTER_NAME", "FILTER_SLOT_NAME_" + FilterSlot, value);
				}
				catch
				{
				}
			}
		}
        #endregion
    }
}
