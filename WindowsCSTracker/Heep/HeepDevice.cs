﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Heep
{
	public class HeepDevice
	{
		private const int FirmwareVersion = 1;

		private List<Byte> deviceMemory = new List<Byte> ();
		private List<Vertex> vertices = new List<Vertex>(); 
		private List<Control> controls = new List<Control>();

		UdpClient interruptServer;

		private const int deviceMemorySize = 200;

		private DeviceID myID;

		public HeepDevice (DeviceID theID)
		{
			SetDeviceID (theID);
			interruptServer = HeepCommunications.GetHeepInterruptServer ();
		}

		public void StartListening()
		{
            HeepCommunications.SendDeviceContext(this);
			HeepCommunications.StartHeepServer (this, interruptServer);
		}	

		public void CloseDevice()
		{
			interruptServer.Close ();
		}

        public List<Control> GetControlList()
        {
            return controls;
        }

		public int GetFirmwareVersion()
		{
			return FirmwareVersion;
		}

		public DeviceID GetDeviceID()
		{
			return myID;
		}

		public void SetDeviceID(DeviceID newID)
		{
			myID = newID;
		}

		public void AddControl(Control newControl)
		{
			newControl.SetID (controls.Count);
			controls.Add (newControl);
		}

		public void AddVertex(Vertex newVertex)
		{
			vertices.Add (newVertex);
			HeepLanguage.AddVertexToMemory (deviceMemory, newVertex);
			NonVolatileData.WriteMemoryToFile (deviceMemory);
		}

		private void AddNewAnalyticsDataToDeviceMemory(Control changedControl)
		{
			if (changedControl.ShouldKeepAnalytics ()) {
                Console.WriteLine("Hello");

				List <byte> analyticsBuffer = HeepDeviceAnalytics.GetDeviceAnalyticsByteArray(changedControl, myID);
				HeepLanguage.AddBufferToBuffer (deviceMemory, analyticsBuffer);
                NonVolatileData.WriteMemoryToFile(deviceMemory);

                HeepCommunications.SendAnalytics (myID, deviceMemory);

            }
		}

		public void SetControlByID(int ID, int newValue, bool trackAnalytics = true)
		{
			for (int i = 0; i < controls.Count; i++) {
				if (controls [i].GetID () == ID) {
					controls[i].SetCurValue(newValue);

					if(trackAnalytics)
						AddNewAnalyticsDataToDeviceMemory (controls[i]);

					SendOutput (controls [i]);
				}
			}
		}

		public void SetControlByName(String controlName, int newValue, bool trackAnalytics = true)
		{
			for (int i = 0; i < controls.Count; i++) {
				if (controls [i].GetName () == controlName) {
					controls [i].SetCurValue (newValue);

					if(trackAnalytics)
						AddNewAnalyticsDataToDeviceMemory (controls[i]);

					SendOutput (controls [i]);
				}
			}
		}

		public void SetControlBufferByID(int ID, List<byte> buffer, bool trackAnalytics = true)
		{
			for (int i = 0; i < controls.Count; i++) {
				if (controls [i].GetID () == ID) {

					if (controls [i].GetType ().Equals (typeof(BufferControl))) {
						BufferControl myCtrlPtr = (BufferControl)controls [i];
						myCtrlPtr.SetBuffer(buffer);
					} else {
						//Debug.Log ("Control is Incorrect Type");
					}

					if(trackAnalytics)
						AddNewAnalyticsDataToDeviceMemory (controls[i]);

					SendOutput (controls [i]);
				}
			}
		}

		private void SendOutput(Control toSend)
		{
			
			// Send the current value of the control
			if (toSend.GetControlDirection () == Control.CtrlInputOutput.output) {
				for (int i = 0; i < vertices.Count; i++) {
					if (vertices [i].GetTXControlID() == toSend.GetID ()) {

						List <byte> sendBuffer;
						if (toSend.GetType ().Equals (typeof(BufferControl))) {
							sendBuffer = HeepLanguage.GetSetValCOPBufferControlBuffer(vertices[i].GetRXControlID(), ((BufferControl)(toSend)).GetBuffer());
						} else {
							sendBuffer = HeepLanguage.GetSetValCOPBuffer (vertices [i].GetRXControlID (), toSend.GetCurValue ());
						}
							
						IPAddress sendIP = vertices [i].GetDestIP ();
						HeepCommunications.SendBufferToIP (sendBuffer, sendIP);
					}
				}
			}
		}

		public int GetControlValueByID(int ID)
		{
			for (int i = 0; i < controls.Count; i++) {
				if (controls [i].GetID () == ID) {
					return controls[i].GetCurValue();
				}
			}

			return -1;
		}

		public List<byte> GetControlBufferByID(int ID)
		{
			for (int i = 0; i < controls.Count; i++) {
				if (controls [i].GetID () == ID) {
					
					if (controls [i].GetType ().Equals (typeof(BufferControl))) {
						BufferControl myCtrlPtr = (BufferControl)controls [i];
						return myCtrlPtr.GetBuffer ();
					} else {
						//Debug.Log ("Control is Incorrect Type");
						return new List<byte>();
					}

				}
			}

			return new List<byte>();
		}

		public void SetDeviceNameStartup(String name)
		{
			if (!HeepParser.DeviceNameOpCodeAlreadySet (deviceMemory)) {
				SetDeviceName (name);
			}
		}

		public void SetDeviceName(String name)
		{
			HeepLanguage.AddNameToMemory (deviceMemory, myID, name);
			NonVolatileData.WriteMemoryToFile (deviceMemory);
		}

		public List <byte> GetMemoryDump()
		{
			List <byte> memDumpOutput = new List<byte>();
			HeepLanguage.GetMemoryDump (memDumpOutput, myID, FirmwareVersion, controls, deviceMemory, deviceMemorySize);

			return memDumpOutput;
		}

		public void LoadDeviceMemoryFromFile()
		{
			deviceMemory = NonVolatileData.ReadMemoryFromFile ();

			vertices = HeepParser.GetVerticesFromBuffer (deviceMemory);
		}
	}

	public class DeviceID
	{
		List<byte> theID;
		private int DeviceIDSize = 4;

		private static int defaultIDSize = 4;

		public DeviceID(List <byte> newID)
		{
			theID = newID;
		}

		public List<byte> GetIDArray()
		{
			return theID;
		}

		public void SetIDArray(List <byte> idArray)
		{
			theID = idArray;
		}

		public int GetDeviceIDSize()
		{
			return DeviceIDSize;
		}

		public static int GetDefaultIDSize()
		{
			return defaultIDSize;
		}

		public static bool operator ==(DeviceID id1, DeviceID id2)
		{
			if (id1.GetDeviceIDSize () != id2.GetDeviceIDSize ()) {
				return false;
			}

			for (int i = 0; i < id1.GetDeviceIDSize (); i++) {
				if (id1.GetIDArray () [i] != id2.GetIDArray () [i])
					return false;
			}

			return true;
		}

		public static bool operator !=(DeviceID id1, DeviceID id2)
		{
			return !(id1 == id2);
		}

	}

	public class Vertex
	{

		DeviceID _rxID;
		DeviceID _txID;

		int _rxControlID;
		int _txControlID;
		IPAddress _destIP;

		public Vertex(DeviceID rxID, DeviceID txId, int rxControlID, int txControlID, IPAddress destIP)
		{
			_rxID = rxID;
			_txID = txId;
			_rxControlID = rxControlID;
			_txControlID = txControlID;
			_destIP = destIP;
		}

		public DeviceID GetTXID()
		{
			return _txID;
		}

		public DeviceID GetRXID()
		{
			return _rxID;
		}

		public int GetRXControlID()
		{
			return _rxControlID;
		}

		public int GetTXControlID()
		{
			return _txControlID;
		}

		public IPAddress GetDestIP()
		{
			return _destIP;
		}
	}

	public class Control
	{
		public enum CtrlInputOutput : int {input = 0, output = 1}; 
		public enum CtrlType : int {OnOff = 0, range = 1, buffer = 2};

		protected int _controlID;
		protected CtrlInputOutput _controlDirection;
		protected CtrlType _controlType;
		protected int _highValue;
		protected int _lowValue;
		protected int _curValue;
		protected String _controlName;
		protected bool _KeepAnalytics;

		public Control(int controlID, CtrlInputOutput controlDirection, CtrlType controlType, int highValue, int lowValue, int curValue, String ControlName, bool KeepAnalytics)
		{
			_controlID = controlID;
			_controlDirection = controlDirection;
			_controlType = controlType;
			_highValue = highValue;
			_lowValue = lowValue;
			_curValue = curValue;
			_controlName = ControlName;
			_KeepAnalytics = KeepAnalytics;
		}

		public void SetID(int controlID)
		{
			_controlID = controlID;
		}

		public int GetID()
		{
			return _controlID;
		}

		public static Control CreateControl (CtrlInputOutput controlDirection, CtrlType controlType, String controlName, int highValue, int lowValue, int curValue, bool KeepAnalytics = true)
		{
			return new Control (0, controlDirection, controlType, highValue, lowValue, curValue, controlName, KeepAnalytics);
		}

		public static Control CreateControl(CtrlInputOutput controlDirection, CtrlType controlType, String controlName, bool KeepAnalytics = true)
		{
			return CreateControl (controlDirection, controlType, controlName, 1, 0, 0, KeepAnalytics);
		}

		public void SetCurValue(int value)
		{
			_curValue = value;
		}

		public int GetCurValue()
		{
			return _curValue;
		}

		public int GetHighValue()
		{
			return _highValue;
		}

		public int GetLowValue()
		{
			return _lowValue;
		}

		public String GetName()
		{
			return _controlName;
		}

		public CtrlType GetControlType()
		{
			return _controlType;
		}

		public CtrlInputOutput GetControlDirection()
		{
			return _controlDirection;
		}

		public bool ShouldKeepAnalytics()
		{
			return _KeepAnalytics;
		}
	}

	public class BufferControl : Control
	{
		protected List <byte> curBuffer;

		public BufferControl(int controlID, CtrlInputOutput controlDirection, CtrlType controlType, int highValue, int lowValue, int curValue, String ControlName, bool sendAnalytics)
			: base (controlID, controlDirection, controlType, highValue, lowValue, curValue, ControlName, sendAnalytics)
		{
		}

		public List<byte> GetBuffer()
		{
			return curBuffer;
		}

		public void SetBuffer(List <byte> newBuffer)
		{
			curBuffer = newBuffer;
		}
	}
}

