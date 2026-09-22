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
    public class INDIDomeNumberEventArgs : IsNewNumberEventArgs
    {
        public INDIDomeNumberType Type;
        public List<INDINumber> Values;
        public INDIDomeNumberEventArgs(INumberVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "ABS_DOME_POSITION":
                    Type = INDIDomeNumberType.AbsPosition;
                    break;
                case "REL_DOME_POSITION":
                    Type = INDIDomeNumberType.RelPosition;
                    break;
                case "DOME_MEASUREMENTS":
                    Type = INDIDomeNumberType.Measurements;
                    break;
                case "DOME_SPEED":
                    Type = INDIDomeNumberType.Speed;
                    break;
                case "DOME_TIMER":
                    Type = INDIDomeNumberType.Timer;
                    break;
                case "DOME_PARAMS":
                    Type = INDIDomeNumberType.Params;
                    break;

                case "TIME_LST":
                    Type = INDIDomeNumberType.TimeLst;
                    break;
                case "GEOGRAPHIC_COORD":
                    Type = INDIDomeNumberType.GeographicCoord;
                    break;
                case "ATMOSPHERE":
                    Type = INDIDomeNumberType.Atmosphere;
                    break;
                default:
                    Type = INDIDomeNumberType.Other;
                    break;
            }
        }
    }
    public class INDIDomeSwitchEventArgs : IsNewSwitchEventArgs
    {
        public INDIDomeSwitchType Type;
        public List<INDISwitch> Values;
        public INDIDomeSwitchEventArgs(ISwitchVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "DOME_MOTION":
                    Type = INDIDomeSwitchType.Motion;
                    break;
                case "DOME_ABORT_MOTION":
                    Type = INDIDomeSwitchType.AbortMotion;
                    break;
                case "DOME_SHUTTER":
                    Type = INDIDomeSwitchType.Shutter;
                    break;
                case "DOME_GOTO":
                    Type = INDIDomeSwitchType.Goto;
                    break;
                case "DOME_AUTOSYNC":
                    Type = INDIDomeSwitchType.Autosync;
                    break;

                case "CONNECTION":
                    Type = INDIDomeSwitchType.Connection;
                    break;
                case "UPLOAD_MODE":
                    Type = INDIDomeSwitchType.UploadMode;
                    break;
                default:
                    Type = INDIDomeSwitchType.Other;
                    break;
            }
        }
    }
    public class INDIDomeTextEventArgs : IsNewTextEventArgs
    {
        public INDIDomeTextType Type;
        public List<INDIText> Values;
        public INDIDomeTextEventArgs(ITextVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "DEVICE_PORT":
                    Type = INDIDomeTextType.DevicePort;
                    break;
                case "TIME_UTC":
                    Type = INDIDomeTextType.TimeUtc;
                    break;
                case "UPLOAD_SETTINGS":
                    Type = INDIDomeTextType.UploadSettings;
                    break;
                case "ACTIVE_DEVICES":
                    Type = INDIDomeTextType.ActiveDevices;
                    break;
                default:
                    Type = INDIDomeTextType.Other;
                    break;
            }
        }
    }
    #endregion
    #region Enums
    public enum INDIDomeNumberType
    {
        TimeLst,
        GeographicCoord,
        Atmosphere,
        Other,

        Speed,
        Timer,
        RelPosition,
        AbsPosition,
        Params,
        Measurements
    }
    public enum INDIDomeSwitchType
    {
        Connection,
        UploadMode,
        Other,

        Motion,
        AbortMotion,
        Shutter,
        Goto,
        Autosync
    }
    public enum INDIDomeTextType
    {
        DevicePort,
        TimeUtc,
        UploadSettings,
        ActiveDevices,
        Other,
    }
    public enum INDIDomeMotion
    {
        CW = 0,
        CCW,
    };

	public enum INDIDomeShutterState
    {
        OPEN = 0,
        CLOSE,
    };

	public enum INDIDomeGoTo
    {
        HOME = 0,
        PARK,
    };
    #endregion
	public class INDIDome : INDIDevice
    {
        public event EventHandler<INDIDomeNumberEventArgs> IsNewNumber = null;
        public event EventHandler<INDIDomeSwitchEventArgs> IsNewSwitch = null;
        public event EventHandler<INDIDomeTextEventArgs> IsNewText = null;
        #region Constructors / Initialization
        public INDIDome(string name, INDIClient host, bool client = true)
            : base(name, host, client)
        {
            if (!client)
            {
                AddNumberVector(new INumberVector(Name, "DOME_SPEED", "Dome speed", "Motion Control", "rw", "", new List<INDINumber>
            {
                new INDINumber("DOME_SPEED_VALUE", "Dome speed in RPM", "%3.1f", 0.00, 180.0, 0.1, 0.0)
            }));
                AddSwitchVector(new ISwitchVector(Name, "DOME_MOTION", "Move dome", "Motion Control", "rw", "OneOfMany", new List<INDISwitch>
            {
                new INDISwitch("DOME_CW", "Move dome Clockwise", true),
                new INDISwitch("DOME_CCW", "Move dome counter clockwise", false)
            }));
                AddNumberVector(new INumberVector(Name, "DOME_TIMER", "Dome speed", "Motion Control", "rw", "", new List<INDINumber>
            {
                new INDINumber("DOME_TIMER_VALUE", "Move the dome for n milliseconds", "%5.0f", 0.00, 60000.0, 1.0, 0.0)
            }));
                AddNumberVector(new INumberVector(Name, "REL_DOME_POSITION", "Dome Relative position", "Motion Control", "rw", "", new List<INDINumber>
            {
                new INDINumber("DOME_RELATIVE_POSITION", "Move n degrees azimuth in the direction selected", "%3.4f", 0.00, 180.0, 0.0001, 0.0)
            }));
                AddNumberVector(new INumberVector(Name, "ABS_DOME_POSITION", "Dome Absolute position", "Motion Control", "rw", "", new List<INDINumber>
            {
                new INDINumber("DOME_ABSOLUTE_POSITION", "Move dome to n absolute azimuth angle in degrees", "%3.4f", 0.00, 180.0, 0.0001, 0.0)
            }));
                AddSwitchVector(new ISwitchVector(Name, "DOME_ABORT_MOTION", "Abort dome motion", "Motion Control", "rw", "AtMostOne", new List<INDISwitch>
            {
                new INDISwitch("ABORT", "Abort dome motion", false)
            }));
                AddSwitchVector(new ISwitchVector(Name, "DOME_SHUTTER", "Open/Close dome shutter", "Main Control", "rw", "OneOfMany", new List<INDISwitch>
            {
                new INDISwitch("SHUTTER_OPEN", "Open dome shutter", false),
                new INDISwitch("SHUTTER_CLOSE", "Close dome shutter", true)
            }));
                AddSwitchVector(new ISwitchVector(Name, "DOME_GOTO", "", "Main Control", "rw", "AtMostOne", new List<INDISwitch>
            {
                new INDISwitch("DOME_HOME", "Go to home position", false),
                new INDISwitch("DOME_PARK", "Go to park position", true)
            }));
                AddNumberVector(new INumberVector(Name, "DOME_PARAMS", "Dome speed", "Main Control", "rw", "", new List<INDINumber>
            {
                new INDINumber("HOME_POSITION", "Dome home absolute position", "%3.1f", 0.00, 180.0, 0.1, 0.0),
                new INDINumber("PARK_POSITION", "Dome parking absolute position", "%3.1f", 0.00, 180.0, 0.1, 0.0),
                new INDINumber("AUTOSYNC_THRESHOLD", "Slaved dome autosync threshold", "%3.1f", 0.00, 180.0, 0.1, 0.0)
            }));
                AddSwitchVector(new ISwitchVector(Name, "DOME_AUTOSYNC", "Enable/Disable dome slaving", "Main Control", "rw", "OneOfMany", new List<INDISwitch>
            {
                new INDISwitch("DOME_AUTOSYNC_ENABLE", "Enable dome slaving", false),
                new INDISwitch("DOME_AUTOSYNC_DISABLE", "Disable dome slaving", true)
            }));
                AddNumberVector(new INumberVector(Name, "DOME_MEASUREMENTS", "Dome informtations", "Dome Info", "rw", "", new List<INDINumber>
            {
                new INDINumber("DM_DOME_RADIUS", "Dome radius (m)", "%3.3f", 0.00, 50.0, 0.001, 0.0),
                new INDINumber("DOME_SHUTTER_WIDTH", "Dome shutter width (m)", "%3.3f", 0.00, 50.0, 0.001, 0.0),
                new INDINumber("DM_NORTH_DISPLACEMENT", "Displacement to the north of the mount center (m)", "%3.3f", 0.00, 50.0, 0.001, 0.0),
                new INDINumber("DM_EAST_DISPLACEMENT", "Displacement to the east of the mount center (m)", "%3.3f", 0.00, 50.0, 0.001, 0.0),
                new INDINumber("DM_UP_DISPLACEMENT", "UP displacement of the mount center (m)", "%3.3f", 0.00, 50.0, 0.001, 0.0),
                new INDINumber("DM_OTA_OFFSET", "Distance from the optical axis to the mount center (m)", "%3.3f", 0.00, 50.0, 0.001, 0.0)
            }));
                DriverInterface |= DRIVER_INTERFACE.DOME_INTERFACE;
            }
        }
        #endregion

        #region Standard Methods
        public void Move(INDIDomeMotion dir, Double milliseconds)
        {
            Direction = dir;
            try
            {
                SetNumber("DOME_TIMER", "DOME_TIMER_VALUE", milliseconds);
            }
            catch { }
        }
        public void Abort()
        {
            try
            {
                SetSwitch("DOME_ABORT_MOTION", "ABORT", true);
            }
            catch { }
        }
        public void GoHome()
        {
            try
            {
                SetSwitchVector("DOME_GOTO", (Int32)INDIDomeGoTo.HOME);
            }
            catch { }
        }
        public void Park()
        {
            try
            {
                SetSwitchVector("DOME_GOTO", (Int32)INDIDomeGoTo.PARK);
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
                    IsNewNumber?.Invoke(this, new INDIDomeNumberEventArgs(e.Vector, e.Device));
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
                    IsNewSwitch?.Invoke(this, new INDIDomeSwitchEventArgs(e.Vector, e.Device));
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
                    IsNewText?.Invoke(this, new INDIDomeTextEventArgs(e.Vector, e.Device));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        #endregion

        #region Standard Properties
        public Double Speed
        {
            get
            {
                try
                {
                    return GetNumber("DOME_SPEED", "DOME_SPEED_VALUE").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_SPEED", "DOME_SPEED_VALUE", value);
                }
                catch { }
            }
        }

        public Double RelativePosition
        {
            get
            {
                try
                {
                    return GetNumber("REL_DOME_POSITION", "DOME_RELATIVE_POSITION").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("REL_DOME_POSITION", "DOME_RELATIVE_POSITION", value);
                }
                catch { }
            }
        }

        public Double AbsolutePosition
        {
            get
            {
                try
                {
                    return GetNumber("ABS_DOME_POSITION", "DOME_ABSOLUTE_POSITION").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("ABS_DOME_POSITION", "DOME_ABSOLUTE_POSITION", value);
                }
                catch { }
            }
        }

        public Double HomePosition
        {
            get
            {
                try
                {
                    return GetNumber("DOME_PARAMS", "HOME_POSITION").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_PARAMS", "HOME_POSITION", value);
                }
                catch { }
            }
        }

        public Double ParkPosition
        {
            get
            {
                try
                {
                    return GetNumber("DOME_PARAMS", "PARK_POSITION").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_PARAMS", "PARK_POSITION", value);
                }
                catch { }
            }
        }

        public Double AutoSyncThreshold
        {
            get
            {
                try
                {
                    return GetNumber("DOME_PARAMS", "AUTOSYNC_THRESHOLD").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_PARAMS", "HOME_POSITION", value);
                }
                catch { }
            }
        }

        Boolean Slaving
        {
            get
            {
                try
                {
                    return GetSwitch("DOME_AUTOSYNC", "DOME_AUTOSYNC_ENABLE").value;
                }
                catch
                {
                    return false;
                }
            }
            set
            {
                try
                {
                    SetSwitchVector("DOME_AUTOSYNC", value ? 0 : 1);
                }
                catch
                {
                }
            }
        }

        public Double Radius
        {
            get
            {
                try
                {
                    return GetNumber("DOME_MEASUREMENTS", "DM_DOME_RADIUS").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_MEASUREMENTS", "DM_DOME_RADIUS", value);
                }
                catch { }
            }
        }

        public Double ShutterWidth
        {
            get
            {
                try
                {
                    return GetNumber("DOME_MEASUREMENTS", "DOME_SHUTTER_WIDTH").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_MEASUREMENTS", "DOME_SHUTTER_WIDTH", value);
                }
                catch { }
            }
        }

        public Double NorthDisplacement
        {
            get
            {
                try
                {
                    return GetNumber("DOME_MEASUREMENTS", "DM_NORTH_DISPLACEMENT").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_MEASUREMENTS", "DM_NORTH_DISPLACEMENT", value);
                }
                catch { }
            }
        }

        public Double EastDisplacement
        {
            get
            {
                try
                {
                    return GetNumber("DOME_MEASUREMENTS", "DM_EAST_DISPLACEMENT").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_MEASUREMENTS", "DM_EAST_DISPLACEMENT", value);
                }
                catch { }
            }
        }

        public Double UpDisplacement
        {
            get
            {
                try
                {
                    return GetNumber("DOME_MEASUREMENTS", "DM_UP_DISPLACEMENT").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_MEASUREMENTS", "DM_UP_DISPLACEMENT", value);
                }
                catch { }
            }
        }

        public Double OpticsDistance
        {
            get
            {
                try
                {
                    return GetNumber("DOME_MEASUREMENTS", "DM_OTA_OFFSET").value;
                }
                catch { }
                return 0;
            }
            set
            {
                try
                {
                    SetNumber("DOME_MEASUREMENTS", "DM_OTA_OFFSET", value);
                }
                catch { }
            }
        }

        public INDIDomeShutterState ShutterState
        {
            get
            {
                try
                {
                    return (INDIDomeShutterState)GetSwitchVector("DOME_SHUTTER").Index;
                }
                catch
                {
                    return INDIDomeShutterState.CLOSE;
                }
            }
            set
            {
                try
                {
                    SetSwitchVector("DOME_SHUTTER", (Int32)value);
                }
                catch
                {
                }
            }
        }

        public INDIDomeMotion Direction
        {
            get
            {
                try
                {
                    return (INDIDomeMotion)GetSwitchVector("DOME_MOTION").Index;
                }
                catch
                {
                    return INDIDomeMotion.CW;
                }
            }
            set
            {
                try
                {
                    SetSwitchVector("DOME_MOTION", (Int32)value);
                }
                catch
                {
                }
            }
        }
        #endregion
    }
}
