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
using System.Threading;
using System.Net.Sockets;
using System.Xml.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Xml;
using System.Linq;
using System.Text.RegularExpressions;

namespace INDI
{
    #region Event Argument classes
    public class MessageReceivedEventArgs : EventArgs
    {
        public string Message;
        public MessageReceivedEventArgs(string msg)
        {
            Message = msg;
        }
    }

    public class DeviceAddedEventArgs : EventArgs
    {
        public INDIDevice Device;
        public DeviceAddedEventArgs(INDIDevice dev)
        {
            Device = dev;
        }
    }

    public class MessageSentEventArgs : EventArgs
    {
        public string Message;
        public MessageSentEventArgs(string msg)
        {
            Message = msg;
        }
    }

    public class IsNewTextEventArgs : EventArgs
    {
        public ITextVector Vector;
        public string Device;
        public IsNewTextEventArgs(ITextVector vector, string dev)
        {
            Vector = vector;
            Device = dev;
        }
    }

    public class IsNewNumberEventArgs : EventArgs
    {
        public INumberVector Vector;
        public string Device;
        public IsNewNumberEventArgs(INumberVector vector, string dev)
        {
            Vector = vector;
            Device = dev;
        }
    }

    public class IsNewSwitchEventArgs : EventArgs
    {
        public ISwitchVector Vector;
        public string Device;
        public IsNewSwitchEventArgs(ISwitchVector vector, string dev)
        {
            Vector = vector;
            Device = dev;
        }
    }

    public class IsNewBlobEventArgs : EventArgs
    {
        public IBlobVector Vector;
        public string Device;
        public IsNewBlobEventArgs(IBlobVector vector, string dev)
        {
            Vector = vector;
            Device = dev;
        }
    }

    public class IsDelPropertyEventArgs : EventArgs
    {
        public string Vector;
        public string Device;
        public IsDelPropertyEventArgs(string vector, string dev)
        {
            Vector = vector;
            Device = dev;
        }
    }

	public class IsNewMessageEventArgs : EventArgs
	{
		public string Message;
		public string Device;
		public DateTime Timestamp;
		public IsNewMessageEventArgs(string message, DateTime timestamp, string dev)
		{
			Timestamp = timestamp;
			Message = message;
			Device = dev;
		}
	}

    #endregion
    public class INDIClient : IDisposable
    {
        #region Private Variables
        TcpClient client;
        Stream stream;
        Thread ReadThread;
        Thread SendThread;
        Boolean ThreadsRunning;
        String _inputString = "";
        String _outputString = "";
        Queue inputString = new Queue();
        Queue outputString = new Queue();
        Int32 _BufferSize = 0x1000000;
        int _CommandSize = 0x10;
        #endregion
        #region Public Properties
        public Int32 BufferSize
        {
            get
            {
                return _BufferSize;
            }
            set
            {
                _BufferSize = value;
            }
        }
        public Int32 CommandSize
        {
            get
            {
                return _CommandSize;
            }
            set
            {
                _CommandSize = value;
            }
        }
        public Boolean Connected
        {
            get
            {
                if (client != null)
                    return client.Connected;
                return false;
            }
        }
        public String InputString
        {
            get
            {
                return _inputString;
            }
            set
            {
                _inputString = value.Replace("\r", "").Replace("\n", "");
                ReadThread = new Thread(new ParameterizedThreadStart(_readThread));
                ReadThread.IsBackground = true;
                ReadThread.Name = "INDISharp Client read thread";
				ReadThread.Start(_inputString);
            }
        }
		string _lastmessage = "";
		string lastmessage { get { return _lastmessage; } set { if(_lastmessage == "") _lastmessage = value; else _lastmessage = ""; } }
        public String OutputString
        {
            get
            {
                if (outputString.Count > 0)
                    return (string)outputString.Dequeue();
                return "";
            }
            set
            {
				if (!String.IsNullOrEmpty (value)) {
					_outputString += value.Replace ("\r", "").Replace ("\n", "");
					outputString.Enqueue (_outputString);
				}
                _outputString = "";
            }
        }
        #endregion
        #region Public Variables
        public string Name = "";
        public string Address = "127.0.0.1";
        public int Port = 7624;
        public List<INDIDevice> Devices = new List<INDIDevice>();
        public event EventHandler<MessageSentEventArgs> MessageSent;
        public event EventHandler<DeviceAddedEventArgs> DeviceAdded;
        public event EventHandler<IsDelPropertyEventArgs> IsDelProperty;
        public event EventHandler<IsNewBlobEventArgs> IsNewBlob;
        public event EventHandler<IsNewTextEventArgs> IsNewText;
		public event EventHandler<IsNewNumberEventArgs> IsNewNumber;
		public event EventHandler<IsNewSwitchEventArgs> IsNewSwitch;
		public event EventHandler<IsNewMessageEventArgs> IsNewMessage;
        #endregion
        #region Constructors / Initialization

        public INDIClient(string uri)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            Uri u = new Uri("http://" + uri);
            Port = u.Port;
            if (!uri.Contains(":"))
                Port = 7624;
            Address = u.Host;
        }

        public INDIClient(string address, int port)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            Address = address;
            Port = port;
        }

        public INDIClient(Stream s)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            Address = "";
            Port = 0;
            stream = s;
        }

        public INDIClient()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            Address = "";
            Port = 0;
            stream = null;
        }

        public bool Connect(string address = "", int port = 0)
        {
            if (address != "")
                Address = address;
            if (port != 0)
                Port = port;
            ThreadsRunning = true;
            if (Address != "" && Port > 0)
            {
                try
                {
                    client = new TcpClient(Address, Port);
                    if (Connected)
                    {
                        stream = client.GetStream();
                        SendThread = new Thread(_sendThread);
                        SendThread.IsBackground = true;
                        SendThread.Name = "INDISharp Client send thread";
                        SendThread.Start();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: {0}", ex.Message);
                }
            }
			if (stream != null)
            {
                ReadThread = new Thread(new ParameterizedThreadStart(_readThread));
                ReadThread.IsBackground = true;
                ReadThread.Name = "INDISharp Client read thread";
                ReadThread.Start(null);
                return true;
			}
			return false;
        }

        public void Dispose()
        {
            Disconnect();
            Devices.Clear();
        }

        public void Disconnect()
        {
            ThreadsRunning = false;
            Thread.Sleep(100);
            outputString.Clear();
            inputString.Clear();
            if (stream != null)
                stream.Close();
            if (client != null)
                client.Close();
        }
        #endregion
		#region Threads
		void _sendThread()
		{
			Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
			while (ThreadsRunning)
			{
				try
				{
					if (outputString.Count > 0 && Connected)
					{
						string message = OutputString;
						lastmessage = message;
						if (!String.IsNullOrEmpty(message))
						{
							byte[] buffer = Encoding.UTF8.GetBytes(message);
							stream.Write(buffer, 0, buffer.Length);
							MessageSent?.Invoke(this, new MessageSentEventArgs(message));
						}
					}
				}
				catch
				{
					continue;
				}
				Thread.Sleep(100);
			}
		}

		void _readThread(object s)
		{
			Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
			string message = "";
			int tmr = 0;
			while (ThreadsRunning)
			{
				try
				{
					if(s == null) {
						while (((NetworkStream)stream).DataAvailable)
						{
							tmr = 0;
							byte[] buffer = new byte[client.Available];
							stream.Read(buffer, 0, buffer.Length);
							message += RemoveInvalidXmlChars(Encoding.UTF8.GetString(buffer));
                            if(IsValidXml(message))
							{
								ParseXML(message.Replace("\n", "").Replace("\r", ""));
                                message = "";
							}
							Thread.Sleep(300);
                        }
                        if(tmr++ > 5)
                        {
							OutputString = lastmessage;
                            message = "";
                        }
					}
					else {
						ParseXML(((String)s).Replace("\n", "").Replace("\r", ""));
                        break;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine (ex.Message);
					continue;
				}
				Thread.Sleep(100);
			}
		}

        public static bool IsValidXml(string xmlString)
        {
            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml("<document>" + xmlString + "</document>");
                return true;
            }
            catch (Exception e1)
            {
				Console.WriteLine (e1.Message);
                return false;
            }
        }

        bool ParseXML(String xml)
		{
			string action = "";
			string target = "";
			string device = "";
			string name = "";
			string label = "";
			string vectorname = "";
			string vectorlabel = "";
			string group = "";
			string permission = "";
			string rule = "";
			string format = "";
			string length = "";
			string minimum = "";
			string maximum = "";
			string step = "";
			IBlobVector blobvector = new IBlobVector ("", "", "", "", "", "", null);
			List<INDIBlob> blobs = new List<INDIBlob> ();
			ISwitchVector switchvector = new ISwitchVector ("", "", "", "", "", "", null);
			List<INDISwitch> switches = new List<INDISwitch> ();
			INumberVector numbervector = new INumberVector ("", "", "", "", "", "", null);
			List<INDINumber> numbers = new List<INDINumber> ();
			ITextVector textvector = new ITextVector ("", "", "", "", "", "", null);
			List<INDIText> texts = new List<INDIText> ();
			try {
				XmlReaderSettings settings = new XmlReaderSettings ();
				settings.ConformanceLevel = ConformanceLevel.Fragment;
                
				XmlReader reader = XmlReader.Create(new StringReader(RemoveInvalidXmlChars(xml)), settings);

				while (reader.Read ()) {
					try {
						switch (reader.NodeType) {
						case XmlNodeType.Element:
							action = reader.Name.ToLower ().Substring (0, 3);
							target = reader.Name.Substring (3, reader.Name.Length - 3).ToLower ();
							action = action == null ? "" : action;
							target = target == null ? "" : target;
							if (action == "mes") {
								try {
									if (IsNewMessage != null)
										IsNewMessage (this, new IsNewMessageEventArgs (reader.GetAttribute ("message"), DateTime.Parse (reader.GetAttribute ("timestamp")), reader.GetAttribute ("device")));
								} catch {
								}
							}
							if (action == "del" && target.Contains ("property")) {
								try {
									if (IsDelProperty != null && name != "")
										IsDelProperty (this, new IsDelPropertyEventArgs (name, reader.GetAttribute ("device")));
								} catch {
								}
							}
							if (action == "get" && target.Contains ("properties")) {
								try {
									DefineProperties (reader.GetAttribute ("device"));
								} catch {
								}
							}
							if (target.Contains ("vector")) {
								device = reader.GetAttribute ("device");
								vectorname = reader.GetAttribute ("name");
								vectorlabel = reader.GetAttribute ("label");
								group = reader.GetAttribute ("group");
								permission = reader.GetAttribute ("perm");
								rule = reader.GetAttribute ("rule");
								device = device == null ? "" : device;
								vectorname = vectorname == null ? "" : vectorname;
								vectorlabel = vectorlabel == null ? "" : vectorlabel;
								group = group == null ? "" : group;
								permission = permission == null ? "ro" : permission;
								rule = rule == null ? "" : rule;
								if (device == "" || vectorname == "")
									break;
								AddDevice (new INDIDevice (device, this));
								if (target.Contains ("blob")) {
									blobs = new List<INDIBlob> ();
									blobvector = GetDevice (device).GetBlobVector (vectorname);
									if (blobvector == null)
										blobvector = new IBlobVector (device, vectorname, vectorlabel, group, permission, rule, blobs);
									blobvector.Values = blobs;
								}
								if (target.Contains ("switch")) {
									switches = new List<INDISwitch> ();
									switchvector = GetDevice (device).GetSwitchVector (vectorname);
									if (switchvector == null)
										switchvector = new ISwitchVector (device, vectorname, vectorlabel, group, permission, rule, switches);
									switchvector.Values = switches;
								}
								if (target.Contains ("number")) {
									numbers = new List<INDINumber> ();
									numbervector = GetDevice (device).GetNumberVector (vectorname);
									if (numbervector == null)
										numbervector = new INumberVector (device, vectorname, vectorlabel, group, permission, rule, numbers);
									numbervector.Values = numbers;
								}
								if (target.Contains ("text")) {
									texts = new List<INDIText> ();
									textvector = GetDevice (device).GetTextVector (vectorname);
									if (textvector == null)
										textvector = new ITextVector (device, vectorname, vectorlabel, group, permission, rule, texts);
									textvector.Values = texts;
								}
							} else {
								name = reader.GetAttribute ("name");
								label = reader.GetAttribute ("label");
								name = name == null ? "" : name;
								label = label == null ? "" : label;
								if (target.Contains ("blob")) {
									format = reader.GetAttribute ("format");
									length = reader.GetAttribute ("size");
									format = format == null ? "" : format;
									length = length == null ? "1" : length;
								}
								if (target.Contains ("number")) {
									format = reader.GetAttribute ("format");
									minimum = reader.GetAttribute ("min");
									maximum = reader.GetAttribute ("max");
									step = reader.GetAttribute ("step");
									format = format == null ? "" : format;
									minimum = minimum == null ? "1" : minimum;
									maximum = maximum == null ? "1" : maximum;
									step = step == null ? "1" : step;
								}
							}
							break;
						case XmlNodeType.Text:
							if (!target.Contains ("vector")) {
								if (target.Contains ("blob")) {
									try {
										blobs.Add (new INDIBlob (name, label, format, Convert.FromBase64String (reader.Value.Replace ("\n", "")), Int32.Parse (length)));
									}
									catch {
										blobs.Add (new INDIBlob (name, label, format, new byte[Int32.Parse (length)], Int32.Parse (length)));
									}
								}
								if (target.Contains ("switch")) {
									switches.Add (new INDISwitch (name, label, reader.Value.Replace ("\n", "").Contains ("On")));
								}
								if (target.Contains ("number")) {
									numbers.Add (new INDINumber (name, label, format, Double.Parse (minimum), Double.Parse (maximum), Double.Parse (step), Double.Parse (reader.Value.Replace ("\n", ""))));
								}
								if (target.Contains ("text")) {
									texts.Add (new INDIText (name, label, reader.Value.Replace ("\n", "")));
								}
							}
							break;
						case XmlNodeType.XmlDeclaration:
							Console.WriteLine ("<?xml version='1.0'?>");
							break;
						case XmlNodeType.EntityReference:
							Console.WriteLine (reader.Name);
							break;
						case XmlNodeType.EndElement:
							if (reader.Name.ToLower ().Contains ("vector")) {
								if (device == "" || vectorname == "")
									break;
								if (target.Contains ("blob")) {
									if (IsNewBlob != null)
										IsNewBlob (this, new IsNewBlobEventArgs (blobvector, device));
								}
								if (target.Contains ("switch")) {
									if (IsNewSwitch != null)
										IsNewSwitch (this, new IsNewSwitchEventArgs (switchvector, device));
								}
								if (target.Contains ("number")) {
									if (IsNewNumber != null)
										IsNewNumber (this, new IsNewNumberEventArgs (numbervector, device));
								}
								if (target.Contains ("text")) {
									if (IsNewText != null)
										IsNewText (this, new IsNewTextEventArgs (textvector, device));
								}
							}
							break;
						}
					} catch (XmlException ex) {
						Console.WriteLine (ex.Message + Environment.NewLine + ex.StackTrace);
						return false;
					} catch (Exception ex) {
						Console.WriteLine (ex.Message + Environment.NewLine + ex.StackTrace);
						return false;
					}
				}
				reader.Close ();
				return true;
			} catch (Exception ex) {
				Console.WriteLine (ex.Message + Environment.NewLine + ex.StackTrace);
				return false;
			}
		}

		static string RemoveInvalidXmlChars(string text) {
			var validXmlChars = text.Where(ch => XmlConvert.IsXmlChar(ch)).ToArray();
			return new string(validXmlChars);
		}

		static bool IsValidXmlString(string text) {
			try {
				XmlConvert.VerifyXmlChars(text);
				return true;
			} catch {
				return false;
			}
		}
        #endregion
        #region Device releated methods

        public DRIVER_INTERFACE Interfaces = DRIVER_INTERFACE.GENERAL_INTERFACE;

        public void AddDevice(INDIDevice dev)
        {
            if (GetDevice(dev.Name) == null && dev != null && dev.Name != "" && (dev.DriverInterface & Interfaces) == Interfaces)
            {
                Devices.Add(dev);
                if (DeviceAdded != null)
                    DeviceAdded(this, new DeviceAddedEventArgs(GetDevice(dev.Name)));
            }
        }

        public void RemoveDevice(string dev)
        {
            if (GetDevice(dev) != null)
                Devices.Remove(GetDevice(dev));
        }

        public INDIDevice GetDevice(string name)
        {
            foreach (INDIDevice d in Devices)
                if (d.Name == name)
                    return d;
            return null;
        }

        public string QueryProperties()
        {
            string ret =
                new XElement("getProperties",
                    new XAttribute("version", "1.7")).ToString();
            OutputString = ret;
            return ret;
        }

        public string DefineProperties(string device = "")
        {
            string ret = "";
            if (device == "" || device == null)
            {
                foreach (INDIDevice d in Devices)
                    ret += d.DefineProperties();
            }
            else
                ret = GetDevice(device).DefineProperties();
            OutputString = ret;
            return ret;
        }
        #endregion
        #region Stream releated methods, misc

        public Stream GetStream()
        {
            return stream;
        }
        #endregion
    }
}
