using System;
using System.Net;  
using System.Net.Sockets;  
using System.IO;
using System.Text;  
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Google.Cloud.Firestore;

namespace Heep
{
	public class HeepCommunications
	{
		public static int PORT = 5000;

		public static void StartHeepServer(HeepDevice device, UdpClient client)
		{
			Thread t = new Thread (() => StartListening (device, client));
			t.Start();
		}

		// Incoming data from the client.  
		public static string data = null;  

		public static UdpClient GetHeepInterruptServer()
		{
			return new UdpClient (PORT);
		}

		public static void StartListening(HeepDevice device, UdpClient client) {  

			byte[] recData;
			while (true)
			{
				try
				{
					IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
					recData = client.Receive(ref anyIP);
					List <byte> commandData = new List<byte>(recData);

					if(HeepParser.isROP(commandData))
					{
						continue;
					}

					List <byte> fromparser = HeepParser.ParseCommand(commandData, device);

					String printableReturn = "";
					for(int i = 0; i < fromparser.Count; i++)
					{
						printableReturn += fromparser[i] + " ";
					}

					UdpClient udpClientB = new UdpClient();
					anyIP.Port = PORT;
					udpClientB.Send(fromparser.ToArray(), fromparser.Count, anyIP);

				}
				catch (ObjectDisposedException) {
					return;
				}
				catch (Exception err)
				{

				}
			}

		}  

		public static List <byte> SendBufferToIP(List <byte> buffer, IPAddress theAddr)
		{
			UdpClient udpClient = new UdpClient();

			System.Net.IPEndPoint remoteEP = new IPEndPoint(theAddr, 5000);

			//Start sending stuff..
			udpClient.Send(buffer.ToArray(), buffer.Count, remoteEP);

			List <byte> retBuffer = new List<byte> ();
			return retBuffer;
		}

        public static string GetDeviceIDString(DeviceID deviceID)
        {
            List<byte> deviceIDList = deviceID.GetIDArray();

            StringBuilder hex = new StringBuilder(deviceIDList.Count * 2);
            foreach (byte b in deviceIDList)
                hex.AppendFormat("{0:x2}", b);

            string deviceIDString = hex.ToString();

            return deviceIDString;
        }

        public static async void SendDeviceContext(DeviceID deviceID)
        {
            string deviceIDString = GetDeviceIDString(deviceID);
            string project = "heep-3cddb";
            FirestoreDb db = FirestoreDb.Create(project);
            Console.WriteLine("Created Cloud Firestore client with project ID: {0}", project);
            Dictionary<string, object> user = new Dictionary<string, object>
            {
                { "Name", "Engagement Keys" }
            };
            WriteResult writeResult = await db.Collection("DeviceList").Document(deviceIDString).SetAsync(user);
        }

		public static async void SendAnalytics(DeviceID deviceID, List<byte> memoryDump)
		{
            string deviceIDString = GetDeviceIDString(deviceID);

            string analyticsString = HeepParser.GetAnalyticsStringFromMemory(memoryDump);

            if (analyticsString.Length > 0)
            {
                string project = "heep-3cddb";
                FirestoreDb db = FirestoreDb.Create(project);

                Dictionary<string, object> DataDictionary = new Dictionary<string, object>
                {
                    { "Data", analyticsString}
                };
                await db.Collection("DeviceList").Document(deviceIDString).Collection("Analytics").AddAsync(DataDictionary);
            }
        }

        static void POST(string url, string jsonContent) 
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "PUT";
			ServicePointManager.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;

			System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
			Byte[] byteArray = encoding.GetBytes(jsonContent);

			request.ContentLength = byteArray.Length;
			request.ContentType = @"application/json";

			using (Stream dataStream = request.GetRequestStream()) {
				dataStream.Write(byteArray, 0, byteArray.Length);
			}
			long length = 0;
			try {
				using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) {
					length = response.ContentLength;
				}
			}
			catch (WebException ex) {
				//Debug.LogException(ex);
			}
		}

		static bool MyRemoteCertificateValidationCallback(System.Object sender,
			X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			bool isOk = true;
			// If there are errors in the certificate chain,
			// look at each error to determine the cause.
			if (sslPolicyErrors != SslPolicyErrors.None) {
				for (int i=0; i<chain.ChainStatus.Length; i++) {
					if (chain.ChainStatus[i].Status == X509ChainStatusFlags.RevocationStatusUnknown) {
						continue;
					}
					chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
					chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
					chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan (0, 1, 0);
					chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
					bool chainIsValid = chain.Build ((X509Certificate2)certificate);
					if (!chainIsValid) {
						isOk = false;
						break;
					}
				}
			}
			return isOk;
		}


		public HeepCommunications ()
		{
		}


	}


}

