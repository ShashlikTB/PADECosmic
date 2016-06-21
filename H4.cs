using System;
using System.Threading; 
using NetMQ; 
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic; 
namespace H4PADE
{





	public class H4
	{

		public string currentStatus; 
		public byte[] currentStatusRaw;


		Thread updateThread; 

		NetMQ.Sockets.SubscriberSocket client; 
		 
		NetMQ.Sockets.PublisherSocket cmdSocket; 

		string ipAddress; 
		Int16 _recvCmdPort; 
		Int16 _drReadyPort; 


		public enum spillState { 

			WAIT, IN, DATARECEIVE 
		}

		public enum H4States { 

			NOP, WWE, WE, WBE, EE, BT, WBT,EBT, RECV, SEND, DATA, READ, STARTRUN, SPILLCOMPL, ENDRUN, DIE

		}


		public delegate void updatedSpillStatusHandler(object H4Net, string msg, byte[] rawstatus); 

		public event updatedSpillStatusHandler spillUpdated; 



		public H4 (string address, Int16 cmdPort, Int16 drReadyPort)
		{

			ipAddress = address;

			_recvCmdPort = cmdPort; 
			_drReadyPort = drReadyPort; 


		}

		public void connectToH4() { 

			try { 
				client = new NetMQ.Sockets.SubscriberSocket(); 
				var listenerAddress = string.Concat("tcp://",ipAddress, ":", _recvCmdPort.ToString()); 
				System.Console.WriteLine("Connecting to H4Net Listener at: {0}", listenerAddress); 
				client.Connect(listenerAddress); 



				client.Subscribe(""); 

				/*client.ReceiveReady += (sender, e) => {
					byte[] bytes = client.ReceiveFrameBytes ();
					currentStatus = System.Text.Encoding.Default.GetString(bytes).Trim(); 
					currentStatusRaw = bytes;
					Console.WriteLine("Current Status: {0}", currentStatus); 

				};*/

				cmdSocket = new NetMQ.Sockets.PublisherSocket();  

				System.Console.WriteLine("Publishing Commands on {0}", _drReadyPort.ToString()); 
				var publishAddress = String.Concat("tcp://*:",_drReadyPort.ToString()); 
				cmdSocket.Bind(publishAddress); 



				updateThread = new Thread(h4receiver); 
				updateThread.Start(); 

				client.ReceiveReady += new EventHandler<NetMQSocketEventArgs>(asyncReceive); 

			}
			catch (Exception e) {
				System.Console.WriteLine("Exception in connection to h4 {0}", e.ToString());
			}

		}

		public void asyncReceive(object mqclient, NetMQSocketEventArgs e) {


			byte[] bytes = client.ReceiveFrameBytes ();
			currentStatus = System.Text.Encoding.Default.GetString(bytes).Trim(); 
			currentStatusRaw = bytes;
			if (spillUpdated != null) { 
				spillUpdated(this, currentStatus, currentStatusRaw); 
			}

		}


		public void h4receiver() { 
			NetMQ.NetMQPoller socketPoller = new NetMQPoller { client }; 
			System.Console.WriteLine("H4 Receiver Thread Starting up"); 

			socketPoller.Run (); 

		}

		public void sendMsg(string msg) { 
			cmdSocket.SendFrame(msg); 

		}

		public void sendREADY() { 
			System.Console.WriteLine("Sending DR Ready"); 
			cmdSocket.SendFrame("DR_READY\0");  
		}

		public void printUpdate(object daq, string msg, byte[] rawstatus) { 
			System.Console.WriteLine("Updated spill status: {0}", msg);

		}
	}
}

