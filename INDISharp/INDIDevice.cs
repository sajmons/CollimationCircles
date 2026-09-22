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
using System.Reflection;

namespace INDI
{
	public class INDIMessageEventArgs : IsNewMessageEventArgs
	{
		public INDIMessageEventArgs(string message, DateTime timestamp, string dev)
			:base (message, timestamp, dev)
		{
		}
	}
    #region Enums
    public enum INDIUploadMode
    {
        CLIENT = 0,
        LOCAL,
        BOTH
    };
    [Flags]
    public enum DRIVER_INTERFACE
    {
        GENERAL_INTERFACE = 0,         /**< Default interface for all INDI devices */
        TELESCOPE_INTERFACE = (1 << 0),  /**< Telescope interface, must subclass INDI::Telescope */
        CCD_INTERFACE = (1 << 1),  /**< CCD interface, must subclass INDI::CCD */
        GUIDER_INTERFACE = (1 << 2),  /**< Guider interface, must subclass INDI::GuiderInterface */
        FOCUSER_INTERFACE = (1 << 3),  /**< Focuser interface, must subclass INDI::FocuserInterface */
        FILTER_INTERFACE = (1 << 4),  /**< Filter interface, must subclass INDI::FilterInterface */
        DOME_INTERFACE = (1 << 5),  /**< Dome interface, must subclass INDI::Dome */
        GPS_INTERFACE = (1 << 6),  /**< GPS interface, must subclass INDI::GPS */
        WEATHER_INTERFACE = (1 << 7),  /**< Weather interface, must subclass INDI::Weather */
        AO_INTERFACE = (1 << 8),  /**< Adaptive Optics Interface */
        DUSTCAP_INTERFACE = (1 << 9),  /**< Dust Cap Interface */
        LIGHTBOX_INTERFACE = (1 << 10), /**< Light Box Interface */
        DETECTOR_INTERFACE = (1 << 11), /**< Detector interface, must subclass INDI::Detector */
        ROTATOR_INTERFACE = (1 << 12), /**< Rotator interface, must subclass INDI::RotatorInterface */
        SPECTROGRAPH_INTERFACE = (1 << 13), /**< Spectrograph interface */
        AUX_INTERFACE = (1 << 15), /**< Auxiliary interface */
    };
    #endregion
    public class INDIDevice : INDIBaseDevice, IDisposable
	{
        #region Constructors / Initialization
        public INDIDevice(string name, INDIClient host, bool client = true)
            : base(name, host, client)
        {
            Host.AddDevice(this);
			if (!client) {
				AddSwitchVector (new ISwitchVector (Name, "CONNECTION", "Connection", "", "rw", "OneOfMany", new List<INDISwitch> {
					new INDISwitch ("CONNECT", "Connect", true),
					new INDISwitch ("DISCONNECT", "Disconnect", false)
				}));
				AddTextVector (new ITextVector (Name, "DEVICE_PORT", "Connection port", "Connection", "ro", "", new List<INDIText> {
					new INDIText ("PORT", "Connection port", "COM1")
				}));
				AddNumberVector (new INumberVector (Name, "TIME_LST", "Local sidereal time", "Device Properties", "ro", "", new List<INDINumber> {
					new INDINumber ("LST", "Local sidereal time", "%16.0f", 0.0, 800000000.0, 0.0, 0.0)
				}));
				AddTextVector (new ITextVector (Name, "TIME_UTC", "UTC Time & Offset", "Device Properties", "ro", "", new List<INDIText> {
					new INDIText ("UTC", "UTC time", "0"),
					new INDIText ("OFFSET", "UTC offset", "0")
				}));
				AddNumberVector (new INumberVector (Name, "GEOGRAPHIC_COORD", "Earth geodetic coordinate", "Device Properties", "ro", "", new List<INDINumber> {
					new INDINumber ("LAT", "Site latitude", "%2.3f", -90.0, 90.0, 0.0, 0.0),
					new INDINumber ("LONG", "Site longitude", "%2.3f", 0.0, 360.0, 0.0, 0.0),
					new INDINumber ("ELEV", "Site elevation", "%2.3f", 0.0, 360.0, 0.0, 0.0)
				}));
				AddNumberVector (new INumberVector (Name, "ATMOSPHERE", "Weather conditions", "Device Properties", "ro", "", new List<INDINumber> {
					new INDINumber ("TEMPERATURE", "Temperature (K)", "%3.3f", -273.0, 180.0, 0.0, 0.0),
					new INDINumber ("PRESSURE", "Pressure (hPa)", "%5.3f", 0.0, 400.0, 0.0, 0.0),
					new INDINumber ("HUMIDITY", "Humidity (%)", "%3.3f", 0.0, 100.0, 0.0, 0.0)
				}));
				DriverInterface = DRIVER_INTERFACE.GENERAL_INTERFACE;
				DriverExec = Assembly.GetCallingAssembly ().GetName ().Name;
				DriverVersion = Assembly.GetCallingAssembly ().GetName ().Version.ToString ();
				DriverName = Name;
			}
        }
        public void Dispose()
        {
            Connected = false;
            GC.Collect();
        }
        #endregion

        #region Standard Properties
        public Boolean Connected
        {
            get
            {
                try
                {
                    return GetSwitch("CONNECTION", "CONNECT").value;
                }
                catch
                {
                }
                return false;
            }
            set
            {
                try
                {
                    if (value == true)
                        SetSwitchVector("CONNECTION", 0);
                    else
                        SetSwitchVector("CONNECTION", 1);
                }
                catch
                {
                }
            }
        }

        public string DevicePort
        {
            get
            {
                try
                {
                    return GetText("DEVICE_PORT", "PORT").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("DEVICE_PORT", "PORT", value);
                }
                catch { }
            }
        }

        public Double SiderealTime
        {
            get
            {
                try
                {
                    return GetNumber("TIME_LST", "LST").value;
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
                    SetNumber("TIME_LST", "LST", value);
                }
                catch { }
            }
        }

        public DateTime UtcTime
        {
            get
            {
                try
                {
                    return DateTime.Parse(GetText("TIME_UTC", "UTC").value);
                }
                catch
                {
                    return DateTime.UtcNow;
                }
            }
            set
            {
                try
                {
                    SetText("TIME_UTC", "UTC", value.ToShortDateString());
                }
                catch { }
            }
        }

        public DateTime UtcOffset
        {
            get
            {
                try
                {
                    return DateTime.Parse(GetText("TIME_UTC", "OFFSET").value);
                }
                catch
                {
                    return new DateTime(0);
                }
            }
            set
            {
                try
                {
                    SetText("TIME_UTC", "OFFSET", value.ToShortDateString());
                }
                catch { }
            }
        }

        public Double Latitude
        {
            get
            {
                try
                {
                    return GetNumber("GEOGRAPHIC_COORD", "LAT").value;
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
                    SetNumber("GEOGRAPHIC_COORD", "LAT", value);
                }
                catch { }
            }
        }

        public Double Longitude
        {
            get
            {
                try
                {
                    return GetNumber("GEOGRAPHIC_COORD", "Int64").value;
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
                    SetNumber("GEOGRAPHIC_COORD", "Int64", value);
                }
                catch { }
            }
        }

        public Double Elevation
        {
            get
            {
                try
                {
                    return GetNumber("GEOGRAPHIC_COORD", "ELEV").value;
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
                    SetNumber("GEOGRAPHIC_COORD", "ELEV", value);
                }
                catch { }
            }
        }

        public Double AmbientTemperature
        {
            get
            {
                try
                {
                    return GetNumber("ATMOSPHERE", "TEMPERATURE").value;
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
                    SetNumber("ATMOSPHERE", "TEMPERATURE", value);
                }
                catch { }
            }
        }

        public Double AmbientPressure
        {
            get
            {
                try
                {
                    return GetNumber("ATMOSPHERE", "PRESSURE").value;
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
                    SetNumber("ATMOSPHERE", "PRESSURE", value);
                }
                catch { }
            }
        }

        public Double AmbientHumidity
        {
            get
            {
                try
                {
                    return GetNumber("ATMOSPHERE", "HUMIDITY").value;
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
                    SetNumber("ATMOSPHERE", "HUMIDITY", value);
                }
                catch { }
            }
        }
        public INDIUploadMode UploadMode
        {
            get
            {
                try
                {
                    return (INDIUploadMode)GetSwitchVector("UPLOAD_MODE").Index;
                }
                catch
                {
                    return INDIUploadMode.CLIENT;
                }
            }
            set
            {
                try
                {
                    SetSwitchVector("UPLOAD_MODE", (Int32)value);
                }
                catch
                {
                }
            }
        }
        public string UploadDirectory
        {
            get
            {
                try
                {
                    return GetText("UPLOAD_SETTINGS", "UPLOAD_DIR").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("UPLOAD_SETTINGS", "UPLOAD_DIR", value);
                }
                catch { }
            }
        }

        public string UploadPrefix
        {
            get
            {
                try
                {
                    return GetText("UPLOAD_SETTINGS", "UPLOAD_PREFIX").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("UPLOAD_SETTINGS", "UPLOAD_PREFIX", value);
                }
                catch { }
            }
        }

        public string ActiveTelescope
        {
            get
            {
                try
                {
                    return GetText("ACTIVE_DEVICES", "ACTIVE_TELESCOPE").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("ACTIVE_DEVICES", "ACTIVE_TELESCOPE", value);
                }
                catch { }
            }
        }

        public string ActiveCCD
        {
            get
            {
                try
                {
                    return GetText("ACTIVE_DEVICES", "ACTIVE_CCD").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("ACTIVE_DEVICES", "ACTIVE_CCD", value);
                }
                catch { }
            }
        }

        public string ActiveFilterWheel
        {
            get
            {
                try
                {
                    return GetText("ACTIVE_DEVICES", "ACTIVE_FILTER").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("ACTIVE_DEVICES", "ACTIVE_FILTER", value);
                }
                catch { }
            }
        }

        public string ActiveFocuser
        {
            get
            {
                try
                {
                    return GetText("ACTIVE_DEVICES", "ACTIVE_FOCUSER").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("ACTIVE_DEVICES", "ACTIVE_FOCUSER", value);
                }
                catch { }
            }
        }

        public string ActiveDome
        {
            get
            {
                try
                {
                    return GetText("ACTIVE_DEVICES", "ACTIVE_DOME").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("ACTIVE_DEVICES", "ACTIVE_DOME", value);
                }
                catch { }
            }
        }

        public string ActiveGPS
        {
            get
            {
                try
                {
                    return GetText("ACTIVE_DEVICES", "ACTIVE_GPS").value;
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                try
                {
                    SetText("ACTIVE_DEVICES", "ACTIVE_GPS", value);
                }
                catch { }
            }
        }

        public string DriverVersion
        {
            get
            {
                try
                {
                    return GetText("DRIVER_INFO", "DRIVER_VERSION").value;
                }
                catch
                {
                    return "";
                }
            }
            internal set
            {
                try
                {
                    SetText("DRIVER_INFO", "DRIVER_VERSION", value);
                }
                catch { }
            }
        }

        public string DriverName
        {
            get
            {
                try
                {
                    return GetText("DRIVER_INFO", "DRIVER_NAME").value;
                }
                catch
                {
                    return "";
                }
            }
            internal set
            {
                try
                {
                    SetText("DRIVER_INFO", "DRIVER_NAME", value);
                }
                catch { }
            }
        }

        public string DriverExec
        {
            get
            {
                try
                {
                    return GetText("DRIVER_INFO", "DRIVER_EXEC").value;
                }
                catch
                {
                    return "";
                }
            }
            internal set
            {
                try
                {
                    SetText("DRIVER_INFO", "DRIVER_EXEC", value);
                }
                catch { }
            }
        }

        public DRIVER_INTERFACE DriverInterface
        {
            get
            {
                try
                {
                    DRIVER_INTERFACE intf = DRIVER_INTERFACE.GENERAL_INTERFACE;
                    if(!Enum.TryParse(GetText("DRIVER_INFO", "DRIVER_INTERFACE").value, out intf))
                        return DRIVER_INTERFACE.GENERAL_INTERFACE;
                    return intf;
                }
                catch
                {
                    return DRIVER_INTERFACE.GENERAL_INTERFACE;
                }
            }
            internal set
            {
                try
                {
                    SetText("DRIVER_INFO", "DRIVER_INTERFACE", value.ToString());
                }
                catch { }
            }
        }
        #endregion
    }
}
