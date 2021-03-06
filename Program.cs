﻿using System;
using System.Timers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using Mono.Options;


namespace PADECosmicRay
{
    class PADE
    {



        public PADE()
        {

            pades = new List<padeStatus>();


        }





        public void parseStatusLine(string status)
        {

            char[] delims = { ' ' };
            var split = status.Trim().Split(delims);

            if (((split.Length - 1) % 2) != 0)
            {
                Console.WriteLine("Bad status message");
                return;
            }

            npades = Convert.ToInt32(split[0]);
            if (npades == 0)
            {
                Console.WriteLine("NPades == 0!");
                return;
            }

            int i = 0;
            while (i < npades)
            {
                var stat = new padeStatus();

                stat.padeType = split[(i * 9) + 1];
                stat.padeID = Convert.ToInt32(split[(i * 9) + 2]);
                stat.status = Convert.ToInt32(split[(i * 9) + 3], 16);

                //byte reg =  Byte.Parse (split [(i * 9) + 2], NumberStyles.AllowHexSpecifier);

                var armed = Convert.ToInt32(split[(i * 9) + 4], 16);
                if (armed > 0)
                    stat.armed = true;
                stat.triggerCount = Convert.ToInt32(split[(i * 9) + 5], 16);
                stat.memoryError = Convert.ToInt32(split[(i * 9) + 6], 16);

                stat.currentTrigger = Convert.ToInt32(split[(i * 9) + 7], 16);
                stat.padeTemperature = Convert.ToInt32(split[(i * 9) + 8], 16);
                stat.sibTemperature = Convert.ToInt32(split[(i * 9) + 9], 16);
                i += 1;
                pades.Add(stat);
            }

        }

        public class padeStatus
        {
            public string padeType;
            public int padeID;
            public int status;
            public bool armed;
            public int triggerCount;
            public int memoryError;
            public int currentTrigger;
            public int padeTemperature;
            public int sibTemperature;


        }

        public List<padeStatus> pades;
        public int npades;

    };





    class MainClass
    {
        private static string fileName;
        private static int currentRun;
        private static bool readingOutput;
        private static object receiveLock;
        private static IPEndPoint e;
        private static UdpClient dataReceiver;
        private static System.Timers.Timer cbTimer;
        private static bool armed;
       
        public static int timeOut = 10;
        private static int fileCount = 0;
        private static Thread recvThread;
        private static uint triggerLimit = 500;

        
        public static LinkedList<Event> Events;
        public static string[] spill_status;


        public class Event
        {
            public int EvNum;
            public int FrameNum;
            public int HitCount;
          
            public int BoardId;
            public int[,] Channels;
            public long[] Ticks;
            public byte[,] RawBytes;
            public Event()
            {
                Channels = new int[32, 128];
                Ticks = new long[32];
                RawBytes = new byte[32, 266];
                for (int i = 0; i < 32; i++)
                { RawBytes[i, 0] = 99; }
            }
        }



        public static void ParseInput(byte[] pack, ref Event evt, ref bool complete)
        {
            if (pack[0] == 1) //data
            {
                complete = false;
                evt.FrameNum = pack[4] * 256 + pack[5];
                int ch = pack[6];
                evt.HitCount = pack[9] * 256 + pack[8];

                for (int i = 0; i < 60; i++)
                {
                    evt.Channels[ch, 2 * i + 1] = pack[15 + 4 * i] * 256 + pack[14 + 4 * i];
                    evt.Channels[ch, 2 * i] = pack[17 + 4 * i] * 256 + pack[16 + 4 * i];
                }
                if (ch == 31) { complete = true; }
            }
            else
            {
                complete = true;
            }


        }


        static void processEvents()
        {
            try
            {
                var fs = new StreamWriter(fileName, true);
                fs.BaseStream.Seek(fs.BaseStream.Length, 0);

                // spill headers
                string[] spill_stat;
                RC_client.GetStatus(out spill_stat);
                spill_status = spill_stat;

                fs.WriteLine("*** starting spill num " + fileCount.ToString() + " *** at " + DateTime.Now.ToString());
                Console.WriteLine("Spill Status Length: {0}", spill_stat.Length);
                for (int i = 0; i < spill_status.Length - 1; i++)
                {

                    if (i == 0) Console.WriteLine(spill_status[i]);
                    fs.WriteLine("*** spill status " + i + " " + spill_status[i] + " ***");
                }
                Console.WriteLine("Writing (approximately) {0} events to output file", Events.Count);

                Event thisEvt;
                var nodes = Events.First;

                for (int i = 0; i < Events.Count; i++)
                {
                    if (nodes != null)
                    {
                        thisEvt = nodes.Value;
                        for (int j = 0; j < 32; j++)
                        {
                            if (thisEvt.RawBytes[j, 0] < 32)
                            {
                                string t = thisEvt.Ticks[j].ToString();
                                // Data from pade by byte: ts_siz[2] boardId[1] pktCount[3] chNum[1] nothing[1] eventLSB[1] eventMSB[1]
                                // prevopuly had: for (int k = 0; k < 9; k++) -> this put nothing in eventMSB[1] and LSB in MSB position
                                // the following fix provides backeard compatibility w/ our Shashlik data files
                                for (int k = 0; k < 7; k++)
                                {
                                    t += " " + thisEvt.RawBytes[j, k].ToString("X2");
                                }
                                // now: ts_siz[2] boardId[1] pktCount[3] chNum[1] nothing[1] eventMSB[1] eventLSB[1] 
                                t += " " + thisEvt.RawBytes[j, 9].ToString("X2");
                                t += " " + thisEvt.RawBytes[j, 8].ToString("X2");
                                for (int k = 0; k < 120; k++)
                                {
                                    t += " " + thisEvt.Channels[j, k].ToString("X3");
                                }
                                fs.WriteLine(t);
                                //lblDaqMessage2.Text = i.ToString();
                            }

                        }
                    }
                    nodes = nodes.Next;
                }
                fs.Flush();
                fs.Close();
                Console.WriteLine("Writing Complete");
				fileCount++; 
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception {0} when processing packets", e.ToString());

            }

        }

        static void processPackets(LinkedList<Byte[]> packets)
        {
            if (packets.Count == 0) return;
            Event evt = new Event();
            bool event_complete = false;
            foreach (Byte[] packet in packets)
            {
                ParseInput(packet, ref evt, ref event_complete);
                if (packet[0] == 1)
                {
                    int ch = Convert.ToInt32(packet[6]);
                    evt.BoardId = (int)packet[2];
                    evt.Ticks[ch] = DateTime.Now.Ticks;
                    for (int i = 0; i < 266; i++)
                    {
                        if (i < packet.Length) { evt.RawBytes[ch, i] = packet[i]; }
                        else { evt.RawBytes[ch, i] = 0; }
                    }
                    //object o = new object();
                }

                if (event_complete)
                {
                    evt.EvNum = Events.Count;
                    Events.AddLast(evt);
                    evt = new Event();
                }
            }
            System.Console.WriteLine("Current Event Chunks {0}", Events.Count);
        }


        public static void receiveData()
        {

            LinkedList<Byte[]> packets = new LinkedList<Byte[]>();

            var packetCount = 0;
            dataReceiver.Client.ReceiveBufferSize = 266 * 33000;
            var startTime = DateTime.Now;
            var stopTime = startTime.AddSeconds(timeOut);
            Console.WriteLine("Receive Data");
            lock (receiveLock)
            {
                readingOutput = true;


               
                while (DateTime.Compare(DateTime.Now, stopTime) < 0)
                {
                    if (dataReceiver.Available > 0)
                    {
                        Byte[] receiveBytes = dataReceiver.Receive(ref e);
                        packets.AddLast(receiveBytes);
                        packetCount++;
                        //Console.WriteLine ("packedcount: {0}  dr.Available {1} receiveBytes[0] {2}", 
                        //packetCount, dataReceiver.Available, receiveBytes[0]);
                    }
                    //Console.WriteLine("B: now {0} stop {1}",DateTime.Now.ToLongTimeString(), stopTime.ToLongTimeString());
                }
                try
                {
                    processPackets(packets);

                    processEvents();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to process {0}, continuing", e.ToString());
                }
                Events.Clear();
                readingOutput = false;

            }

            string adc = RC_client.ADC(); 
			if (!(RC_client.checkADC(adc))) {
				System.Console.WriteLine("ADC's returned bad value...quitting"); 
				return; 
			}
            RC_client.Clear();
            //System.Console.WriteLine (adc);
            System.Console.WriteLine("Packet Count: {0}, Packets Count: {1}", packetCount, packets.Count);
            System.Console.WriteLine("Finished run {0}", currentRun);
            System.Console.WriteLine("===============================");
            System.Console.WriteLine("");



        }

        public static void statusCheck(Object source, ElapsedEventArgs e)
        {
            if (readingOutput)
            {
                return;
            }

            if (!armed)
            {
                Console.WriteLine("Arming PADEs");
                RC_client.Arm();
                armed = true;
            }

            PADE p = new PADE();

            // spill headers
            string status = RC_client.RawStatus();
            Console.WriteLine("Current Status: {0}", status);
            p.parseStatusLine(status);

            var count = p.pades[0].triggerCount;
            Console.WriteLine("Current Trigger Count: {0}", count.ToString());
            if (count >= triggerLimit)
            {

                RC_client.Disarm();
                armed = false;
                readingOutput = true;
                recvThread = new Thread(new ThreadStart(receiveData));
                recvThread.Start();
                Thread.Sleep(1);
                RC_client.ReadAll();
                currentRun += 1;

            }

        }

        public static void help()
        {


            Console.WriteLine("Following options are available:");
			Console.WriteLine ("-r, --prefix, Prefix for output files. capture_cosmic_ is the default"); 
            Console.WriteLine("-h, --host, host name for RC Client");
            Console.WriteLine("-p, --port, port for RC Client");
            Console.WriteLine("-f, --folder, outputFolder for data files.");
            Console.WriteLine("-t, --count, TriggerCount limit.");

        }

        public static void Main(string[] args)
        {
            bool show_help = false;
            var host = "192.168.0.225";
            uint port = 23;
            //string outputFolder = "/home/daquser/DAQ";
			string outputFolder = ".";
			string fileBase = "capture_cosmic";
            var p = new OptionSet() {
				{"r|prefix=", "Output file prefix",
					v => { if (v != null) fileBase = v; }
				},
                {"h|host=", "Remote Control Host",
                    v => { if (v != null) host = v; }
                },
                {"p|port=", "Remote Port",
                    (uint v) => { if (v != 0) port = v;}
                },
                {"help", "Show message and exit.",
                    v => show_help = v != null
                },
                {"f|folder=", "Output Folder",
                v => { if (v != null) outputFolder = v; }
                },
                {"t|count=", "Trigger Count Limit.",
                (uint v) => { if (v != 0) triggerLimit = v; }
                }
            };


            //List<string> extra;
            try
            {
                //extra = p.Parse(args);
				p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Bad Option: {0}", e.Message);
                return;
            }

            if (show_help)
            {
                help();
                return;
            }

            Console.WriteLine("Outputting to {0}", outputFolder);


            Console.WriteLine("host: {0}, port: {1}", host, port.ToString());



            Console.WriteLine("Opening connection to PADE Control Software....");
            RC_client.Open(host, port);
            if (!RC_client.ClientOpen)
            {
                Console.WriteLine("Cannot connect to Pade Control Software");
                return;
            }


            string adc = RC_client.ADC(); 
			Console.WriteLine ("Connected!, Current ADC Status: {0}", adc );
			
			if (!RC_client.checkADC(adc)) {

				Console.WriteLine("ADC's not in a good state, check the PADEs and try again"); 
				return; 
			}




            try
            {
                Directory.SetCurrentDirectory(outputFolder);
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine("Couldn't change directory, using current directory for output, error: {0}", ex);
            }
            
			DateTime n = System.DateTime.Now;
			fileName = String.Concat(fileBase, "_", n.Year.ToString() + n.Month.ToString("00") + n.Day.ToString("00")+
				"_"+n.Hour.ToString("00")+n.Minute.ToString("00")+n.Second.ToString("00")+".txt"); 
           // fileName = String.Concat(fileBase, "_", DateTime.Now.Hour.ToString(), DateTime.Now.Minute.ToString(), DateTime.Now.Second.ToString(), ".txt");
            Console.WriteLine("Output File:{0}", fileName);
            Events = new LinkedList<Event>();
             
            IPEndPoint endPoint; 
            try
            {
                endPoint = new IPEndPoint(IPAddress.Any, 21331);
            }
            catch (Exception except)
            {

                Console.WriteLine("Cannot open UDP Port 21331, {0}", except.ToString());
                return;
            }

            Console.WriteLine("Waiting for {0} triggers.", triggerLimit.ToString());

            dataReceiver = new UdpClient(endPoint);
            receiveLock = new object();
            cbTimer = new System.Timers.Timer(1000);
           
            cbTimer.Elapsed += statusCheck;
            cbTimer.Enabled = true;

            System.Console.WriteLine("Press any key to quit");

            System.Console.ReadLine();

            //Make sure we're disarmed before quitting
			//We lock here just in case we're running receive at the same time we try to quit
			lock(receiveLock) 
			{ 


            	RC_client.Disarm();

			}




        }
    }
}
