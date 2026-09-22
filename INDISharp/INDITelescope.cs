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
    public class INDITelescopeNumberEventArgs : IsNewNumberEventArgs
    {
        public INDITelescopeNumberType Type;
        public List<INDINumber> Values;
        public INDITelescopeNumberEventArgs(INumberVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "EQUATORIAL_COORD":
                    Type = INDITelescopeNumberType.EquatorialCoord;
                    break;
                case "EQUATORIAL_EOD_COORD":
                    Type = INDITelescopeNumberType.EquatorialEodCoord;
                    break;
                case "TARGET_EOD_COORD":
                    Type = INDITelescopeNumberType.TargetEodCoord;
                    break;
                case "HORIZONTAL_COORD":
                    Type = INDITelescopeNumberType.HorizontalCoord;
                    break;
                case "TELESCOPE_TIMED_GUIDE_NS":
                    Type = INDITelescopeNumberType.TimedGuideNS;
                    break;
                case "TELESCOPE_TIMED_GUIDE_WE":
                    Type = INDITelescopeNumberType.TimedGuideWE;
                    break;
                case "TELESCOPE_PARK_POSITION":
                    Type = INDITelescopeNumberType.ParkPosition;
                    break;
                case "TELESCOPE_INFO":
                    Type = INDITelescopeNumberType.Informations;
                    break;

                case "TIME_LST":
                    Type = INDITelescopeNumberType.TimeLst;
                    break;
                case "GEOGRAPHIC_COORD":
                    Type = INDITelescopeNumberType.GeographicCoord;
                    break;
                case "ATMOSPHERE":
                    Type = INDITelescopeNumberType.Atmosphere;
                    break;
                default:
                    Type = INDITelescopeNumberType.Other;
                    break;
            }
        }
    }
    public class INDITelescopeSwitchEventArgs : IsNewSwitchEventArgs
    {
        public INDITelescopeSwitchType Type;
        public List<INDISwitch> Values;
        public INDITelescopeSwitchEventArgs(ISwitchVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "ON_COORD_SET":
                    Type = INDITelescopeSwitchType.OnCoordSet;
                    break;
                case "TELESCOPE_MOTION_NS":
                    Type = INDITelescopeSwitchType.MotionNS;
                    break;
                case "TELESCOPE_MOTION_WE":
                    Type = INDITelescopeSwitchType.MotionWE;
                    break;
                case "TELESCOPE_SLEW_RATE":
                    Type = INDITelescopeSwitchType.SlewRate;
                    break;
                case "TELESCOPE_PARK":
                    Type = INDITelescopeSwitchType.Park;
                    break;
                case "TELESCOPE_PARK_OPTION":
                    Type = INDITelescopeSwitchType.ParkOption;
                    break;
                case "TELESCOPE_ABORT_MOTION":
                    Type = INDITelescopeSwitchType.AbortMotion;
                    break;
                case "TELESCOPE_TRACK_RATE":
                    Type = INDITelescopeSwitchType.TrackRate;
                    break;
                case "TELESCOPE_PIER_SIDE":
                    Type = INDITelescopeSwitchType.PierSide;
                    break;

                case "CONNECTION":
                    Type = INDITelescopeSwitchType.Connection;
                    break;
                case "UPLOAD_MODE":
                    Type = INDITelescopeSwitchType.UploadMode;
                    break;
                default:
                    Type = INDITelescopeSwitchType.Other;
                    break;
            }
        }
    }
    public class INDITelescopeTextEventArgs : IsNewTextEventArgs
    {
        public INDITelescopeTextType Type;
        public List<INDIText> Values;
        public INDITelescopeTextEventArgs(ITextVector vector, string dev) : base(vector, dev)
        {
            Values = vector.Values;
            switch (vector.Name)
            {
                case "DEVICE_PORT":
                    Type = INDITelescopeTextType.DevicePort;
                    break;
                case "TIME_UTC":
                    Type = INDITelescopeTextType.TimeUtc;
                    break;
                case "UPLOAD_SETTINGS":
                    Type = INDITelescopeTextType.UploadSettings;
                    break;
                case "ACTIVE_DEVICES":
                    Type = INDITelescopeTextType.ActiveDevices;
                    break;
                default:
                    Type = INDITelescopeTextType.Other;
                    break;
            }
        }
    }
    #endregion
    #region Enums
    public enum INDITelescopeNumberType
    {
        TimeLst,
        GeographicCoord,
        Atmosphere,
        Other,

        EquatorialCoord,
        EquatorialEodCoord,
        TargetEodCoord,
        HorizontalCoord,
        TimedGuideNS,
        TimedGuideWE,
        ParkPosition,
        Informations,
    }
    public enum INDITelescopeSwitchType
    {
        Connection,
        UploadMode,
        Other,

        OnCoordSet,
        MotionNS,
        MotionWE,
        SlewRate,
        Park,
        ParkOption,
        AbortMotion,
        TrackRate,
        PierSide,
    }
    public enum INDITelescopeTextType
    {
        DevicePort,
        TimeUtc,
        UploadSettings,
        ActiveDevices,
        Other,
    }
    public enum INDICoordSet
    {
        SLEW = 0,
        TRACK,
        SYNC
    };
    public enum INDIDirection
    {
        NORTH = 0,
        SOUTH,
        WEST,
        EAST,
        NONE
    };
    public enum INDIRate
    {
        GUIDE = 0,
        CENTERING,
        FIND,
        MAX
    };
    public enum INDITrackRate
    {
        SIDEREAL = 0,
        SOLAR,
        LUNAR,
        CUSTOM
    };
    #endregion
    public class INDITelescope : INDIDevice
    {
        public event EventHandler<INDITelescopeNumberEventArgs> IsNewNumber = null;
        public event EventHandler<INDITelescopeSwitchEventArgs> IsNewSwitch = null;
        public event EventHandler<INDITelescopeTextEventArgs> IsNewText = null;
        #region Constructors / Initialization
        public INDITelescope(string name, INDIClient host, bool client = true)
            : base(name, host, client)
		{
			if (!client) {
				AddNumberVector (new INumberVector (Name, "EQUATORIAL_COORD", "Equatorial astrometric J2000 coordinate", "Main Control", "rw", "", new List<INDINumber> {
					new INDINumber ("RA", "J2000 RA", "%2.12f", 0.00, 24.0, 0.000000000001, 0.0),
					new INDINumber ("DEC", "J2000 Dec", "%2.12f", -90.00, 90.0, 0.000000000001, 0.0)
				}));
				AddNumberVector (new INumberVector (Name, "EQUATORIAL_EOD_COORD", "Equatorial astrometric epoch of date coordinate", "Main Control", "rw", "", new List<INDINumber> {
					new INDINumber ("RA", "JNow RA", "%2.12f", 0.00, 24.0, 0.000000000001, 0.0),
					new INDINumber ("DEC", "JNow Dec", "%2.12f", -90.00, 90.0, 0.000000000001, 0.0)
				}));
				AddNumberVector (new INumberVector (Name, "TARGET_EOD_COORD", "Slew Target", "Main Control", "ro", "Main Control", new List<INDINumber> {
					new INDINumber ("RA", "JNow RA", "%2.12f", 0.00, 24.0, 0.000000000001, 0.0),
					new INDINumber ("DEC", "JNow Dec", "%2.12f", -90.00, 90.0, 0.000000000001, 0.0)
				}));
				AddNumberVector (new INumberVector (Name, "HORIZONTAL_COORD", "topocentric coordinate", "Main Control", "ro", "Main Control", new List<INDINumber> {
					new INDINumber ("ALT", "Altitude", "%2.12f", 0.00, 90.0, 0.000000000001, 0.0),
					new INDINumber ("AZ", "Azimuth", "%3.12f", 0.00, 360.0, 0.000000000001, 0.0)
				}));
				AddSwitchVector (new ISwitchVector (Name, "ON_COORD_SET", "Track mode", "Main Control", "rw", "OneOfMany", new List<INDISwitch> {
					new INDISwitch ("SLEW", "Slew", true),
					new INDISwitch ("TRACK", "Track", false),
					new INDISwitch ("SYNC", "Sync", false)
				}));
				AddSwitchVector (new ISwitchVector (Name, "TELESCOPE_MOTION_NS", "Move telescope north or south", "Motion Control", "rw", "OneOfMany", new List<INDISwitch> {
					new INDISwitch ("MOTION_NORTH", "Move North", false),
					new INDISwitch ("MOTION_SOUTH", "Move South", false)
				}));
				AddSwitchVector (new ISwitchVector (Name, "TELESCOPE_MOTION_WE", "Move telescope west or east", "Motion Control", "rw", "OneOfMany", new List<INDISwitch> {
					new INDISwitch ("MOTION_WEST", "Move West", false),
					new INDISwitch ("MOTION_EAST", "Move East", false)
				}));
				AddNumberVector (new INumberVector (Name, "TELESCOPE_TIMED_GUIDE_NS", "Timed guide telescope north or south", "Motion Control", "rw", "", new List<INDINumber> {
					new INDINumber ("TIMED_GUIDE_N", "Guide North", "%5.2f", 0.00, 10000.0, 0.01, 0.0),
					new INDINumber ("TIMED_GUIDE_S", "Guide South", "%2.2f", 0.00, 10000.0, 0.01, 0.0)
				}));
				AddNumberVector (new INumberVector (Name, "TELESCOPE_TIMED_GUIDE_WE", "Timed guide telescope west or east", "Motion Control", "rw", "", new List<INDINumber> {
					new INDINumber ("TIMED_GUIDE_W", "Guide North", "%5.2f", 0.00, 10000.0, 0.01, 0.0),
					new INDINumber ("TIMED_GUIDE_E", "Guide South", "%2.2f", 0.00, 10000.0, 0.01, 0.0)
				}));
				AddSwitchVector (new ISwitchVector (Name, "TELESCOPE_SLEW_RATE", "Slew Rate", "Motion Control", "rw", "OneOfMany", new List<INDISwitch> {
					new INDISwitch ("SLEW_GUIDE", "Guiding Rate", false),
					new INDISwitch ("SLEW_CENTERING", "Slow speed", false),
					new INDISwitch ("SLEW_FIND", "Medium speed", true),
					new INDISwitch ("SLEW_MAX", "Maximum speed", false),
				}));
				AddSwitchVector (new ISwitchVector (Name, "TELESCOPE_PARK", "Park and unpark the telescope", "Main Control", "rw", "OneOfMany", new List<INDISwitch> {
					new INDISwitch ("PARK", "Park the telescope", false),
					new INDISwitch ("UNPARK", "Unpark the telescope", false)
				}));
				AddNumberVector (new INumberVector (Name, "TELESCOPE_PARK_POSITION", "Home park position", "Main Control", "ro", "", new List<INDINumber> {
					new INDINumber ("PARK_RA", "JNow RA", "%2.12f", 0.00, 24.0, 0.000000000001, 0.0),
					new INDINumber ("PARK_DEC", "JNow Dec", "%2.12f", -90.00, 90.0, 0.000000000001, 0.0)
				}));
				AddSwitchVector (new ISwitchVector (Name, "TELESCOPE_PARK_OPTION", "Park Option", "Main Control", "rw", "OneOfMany", new List<INDISwitch> {
					new INDISwitch ("PARK_CURRENT", "Use current park position", false),
					new INDISwitch ("PARK_DEFAULT", "Use driver's default park position", true),
					new INDISwitch ("PARK_WRITE_DATA", "Write park position", false)
				}));
				AddSwitchVector (new ISwitchVector (Name, "TELESCOPE_ABORT_MOTION", "Abort", "Main Control", "rw", "AtMostOne", new List<INDISwitch> {
					new INDISwitch ("ABORT_MOTION", "Stop telescope", false)
				}));
				AddSwitchVector (new ISwitchVector (Name, "TELESCOPE_TRACK_RATE", "Track Rate", "Main Control", "rw", "AtMostOne", new List<INDISwitch> {
					new INDISwitch ("TRACK_SIDEREAL", "Track at sidereal rate", true),
					new INDISwitch ("TRACK_SOLAR", "Track at solar rate", false),
					new INDISwitch ("TRACK_LUNAR", "Track at lunar rate", false),
					new INDISwitch ("TRACK_CUSTOM", "Track at custom rate", false),
				}));
				AddNumberVector (new INumberVector (Name, "TELESCOPE_INFO", "Telescope informtations", "Telescope Info", "ro", "Informations", new List<INDINumber> {
					new INDINumber ("TELESCOPE_APERTURE", "Telescope aperture", "%5.2f", 0.00, 10000.0, 0.01, 200.0),
					new INDINumber ("TELESCOPE_FOCAL_LENGTH", "Telescope focal length", "%5.2f", 0.00, 10000.0, 0.01, 200.0),
					new INDINumber ("GUIDER_APERTURE", "Guide telescope aperture", "%5.2f", 0.00, 10000.0, 0.01, 200.0),
					new INDINumber ("GUIDER_FOCAL_LENGTH", "Guide telescope focal length", "%5.2f", 0.00, 10000.0, 0.01, 200.0),
				}));
				AddSwitchVector (new ISwitchVector (Name, "TELESCOPE_PIER_SIDE", "Pier Side", "Motion Control", "rw", "OneOfMany", new List<INDISwitch> {
					new INDISwitch ("PIER_EAST", "Pointing West", false),
					new INDISwitch ("PIER_WEST", "Pointing East", false)
				}));
				DriverInterface |= DRIVER_INTERFACE.TELESCOPE_INTERFACE;
			}
        }
        #endregion

        #region Standard Methods
        public void Move(INDIDirection direction)
        {
            try
            {
                string vector = "TELESCOPE_MOTION_";
                string number = "MOTION_";
                switch (direction)
                {
                    case INDIDirection.NORTH:
                        vector += "NS";
                        number += "N";
                        break;
                    case INDIDirection.SOUTH:
                        vector += "NS";
                        number += "S";
                        break;
                    case INDIDirection.WEST:
                        vector += "WE";
                        number += "W";
                        break;
                    case INDIDirection.EAST:
                        vector += "WE";
                        number += "E";
                        break;
                }
                SetSwitch(vector, number, true);
            }
            catch
            {
            }
        }

        public void Stop(INDIDirection direction)
        {
            try
            {
                string vector = "TELESCOPE_MOTION_";
                string number = "MOTION_";
                switch (direction)
                {
                    case INDIDirection.NORTH:
                        vector += "NS";
                        number += "N";
                        break;
                    case INDIDirection.SOUTH:
                        vector += "NS";
                        number += "S";
                        break;
                    case INDIDirection.WEST:
                        vector += "WE";
                        number += "W";
                        break;
                    case INDIDirection.EAST:
                        vector += "WE";
                        number += "E";
                        break;
                }
                SetSwitch(vector, number, false);
            }
            catch
            {
            }
        }

        public void PulseGuide(INDIDirection direction, Int64 ms)
        {
            string vector = "TELESCOPE_TIMED_GUIDE_";
            string number = "TIMED_GUIDE_";
            switch (direction)
            {
                case INDIDirection.NORTH:
                    vector += "NS";
                    number += "N";
                    break;
                case INDIDirection.SOUTH:
                    vector += "NS";
                    number += "S";
                    break;
                case INDIDirection.WEST:
                    vector += "WE";
                    number += "W";
                    break;
                case INDIDirection.EAST:
                    vector += "WE";
                    number += "E";
                    break;
            }
            try
            {
                SetNumber(vector, number, ms);
            }
            catch
            {
            }
        }

        public void SyncToAltAz(Double Altitude, Double Azimuth)
        {
            CoordSet = INDICoordSet.SYNC;
            Double[] values = { Altitude, Azimuth };
            SetNumberVector("HORIZONTAL_COORD", values);
		}

		public void SlewToAltAz(Double Altitude, Double Azimuth)
		{
			CoordSet = INDICoordSet.SLEW;
			Double[] values = { Altitude, Azimuth };
			SetNumberVector("HORIZONTAL_COORD", values);
		}

		public void TrackToAltAz(Double Altitude, Double Azimuth)
		{
			CoordSet = INDICoordSet.TRACK;
			Double[] values = { Altitude, Azimuth };
			SetNumberVector("HORIZONTAL_COORD", values);
		}

        public void SyncToRaDec(Double RightAscension, Double Declination)
        {
            CoordSet = INDICoordSet.SYNC;
            Double[] values = { RightAscension, Declination };
            SetNumberVector("EQUATORIAL_EOD_COORD", values);
		}

		public void SlewToRaDec(Double RightAscension, Double Declination)
		{
			CoordSet = INDICoordSet.SLEW;
			Double[] values = { RightAscension, Declination };
			SetNumberVector("EQUATORIAL_EOD_COORD", values);
		}

		public void TrackToRaDec(Double RightAscension, Double Declination)
		{
			CoordSet = INDICoordSet.TRACK;
			Double[] values = { RightAscension, Declination };
			SetNumberVector("EQUATORIAL_EOD_COORD", values);
		}

        public void Abort()
        {
            try
            {
                SetSwitch("TELESCOPE_ABORT_MOTION", "ABORT_MOTION", true);
            }
            catch
            {
            }
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
                    IsNewNumber?.Invoke(this, new INDITelescopeNumberEventArgs(e.Vector, e.Device));
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
                    IsNewSwitch?.Invoke(this, new INDITelescopeSwitchEventArgs(e.Vector, e.Device));
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
                    IsNewText?.Invoke(this, new INDITelescopeTextEventArgs(e.Vector, e.Device));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        #endregion

        #region Standard Properties
        public Double RaJ2000
        {
            get
            {
                try
                {
                    return GetNumber("EQUATORIAL_COORD", "RA").value;
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
                    SetNumber("EQUATORIAL_COORD", "RA", value);
                }
                catch
                {
                }
            }
        }

        public Double DecJ2000
        {
            get
            {
                try
                {
                    return GetNumber("EQUATORIAL_COORD", "DEC").value;
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
                    SetNumber("EQUATORIAL_COORD", "DEC", value);
                }
                catch
                {
                }
            }
        }

        public Double Ra
        {
            get
            {
                try
                {
                    return GetNumber("EQUATORIAL_EOD_COORD", "RA").value;
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
                    SetNumber("EQUATORIAL_EOD_COORD", "RA", value);
                }
                catch
                {
                }
            }
        }

        public Double Dec
        {
            get
            {
                try
                {
                    return GetNumber("EQUATORIAL_EOD_COORD", "DEC").value;
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
                    SetNumber("EQUATORIAL_EOD_COORD", "DEC", value);
                }
                catch
                {
                }
            }
        }

        public Double Alt
        {
            get
            {
                try
                {
                    return GetNumber("HORIZONTAL_COORD", "ALT").value;
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
                    SetNumber("HORIZONTAL_COORD", "ALT", value);
                }
                catch
                {
                }
            }
        }

        public Double Az
        {
            get
            {
                try
                {
                    return GetNumber("HORIZONTAL_COORD", "AZ").value;
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
                    SetNumber("HORIZONTAL_COORD", "AZ", value);
                }
                catch
                {
                }
            }
		}

		public Boolean Parked
		{
			get
			{
				try
				{
					return GetSwitch("TELESCOPE_PARK", "PARK").value;
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
					SetSwitchVector("TELESCOPE_PARK", value ? 0 : 1);
				}
				catch
				{
				}
			}
		}

		public Boolean Track
		{
			get
			{
				try
				{
					return GetSwitch("TELESCOPE_TRACK_STATE", "TRACK_ON").value;
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
					SetSwitchVector("TELESCOPE_TRACK_STATE", value ? 0 : 1);
				}
				catch
				{
				}
			}
		}

        public string Diameter
        {
            get
            {
                try
                {
                    return GetText("TELESCOPE_INFO", "TELESCOPE_APERTURE").value;
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
                    SetText("TELESCOPE_INFO", "TELESCOPE_APERTURE", value);
                }
                catch
                {
                }
            }
        }

        public string FocalLenght
        {
            get
            {
                try
                {
                    return GetText("TELESCOPE_INFO", "TELESCOPE_FOCAL_LENGTH").value;
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
                    SetText("TELESCOPE_INFO", "TELESCOPE_FOCAL_LENGTH", value);
                }
                catch
                {
                }
            }
        }

        public string GuiderDiameter
        {
            get
            {
                try
                {
                    return GetText("TELESCOPE_INFO", "GUIDER_APERTURE").value;
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
                    SetText("TELESCOPE_INFO", "GUIDER_APERTURE", value);
                }
                catch
                {
                }
            }
        }

        public string GuiderFocalLenght
        {
            get
            {
                try
                {
                    return GetText("TELESCOPE_INFO", "GUIDER_FOCAL_LENGTH").value;
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
                    SetText("TELESCOPE_INFO", "GUIDER_FOCAL_LENGTH", value);
                }
                catch
                {
                }
            }
        }

        public INDICoordSet CoordSet
        {
            get
            {
                try
                {
                    return (INDICoordSet)GetSwitchVector("ON_COORD_SET").Index;
                }
                catch
                {
                    return INDICoordSet.TRACK;
                }
            }
            set
            {
                try
                {
                    SetSwitchVector("ON_COORD_SET", (Int32)value);
                }
                catch
                {
                }
            }
        }

        public INDIRate SlewRate
        {
            get
            {
                try
                {
                    return (INDIRate)GetSwitchVector("TELESCOPE_SLEW_RATE").Index;
                }
                catch
                {
                    return INDIRate.GUIDE;
                }
            }
            set
            {
                try
                {
                    SetSwitchVector("TELESCOPE_SLEW_RATE", (Int32)value);
                }
                catch
                {
                }
            }
        }

        public INDITrackRate TrackRate
        {
            get
            {
                try
                {
                    return (INDITrackRate)GetSwitchVector("TELESCOPE_TRACK_RATE").Index;
                }
                catch
                {
                    return INDITrackRate.CUSTOM;
                }
            }
            set
            {
                try
                {
                    SetSwitchVector("TELESCOPE_TRACK_RATE", (Int32)value);
                }
                catch
                {
                }
            }
        }
        #endregion
    }
}
