using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Net.Sockets;
using System.IO;


namespace H4PADE
{
    public static class RC_client
    {
        public static bool ClientOpen = false;
        
		private static string rcsAddress = "192.168.0.225";

        private static int rcsPort = 23;

     


        private static TcpClient client;
        private static NetworkStream stream;
        private static StreamReader SR;
        private static StreamWriter SW;
        private static int max_timeout;
        private static int timeout;

		public static void Open(string host, uint port)
        {
            ClientOpen = false;
            try
            {
                Console.WriteLine("Running on host: " + System.Environment.MachineName);
				Console.WriteLine("Connecting to Host: {0}, and Port: {1}", host, port.ToString()); 
				client = new TcpClient(host, (int) port); 
			
                stream = client.GetStream();
                SW = new StreamWriter(stream);
                SR = new StreamReader(stream);
                SW.AutoFlush = true;
                max_timeout = 100;
                ClientOpen = true;
            }
            catch { }
        }

        public static string getRCSAddress()
        {
            return rcsAddress;
        }

        public static int getRCSPort()
        {
            return rcsPort;
        }

        public static bool updateRCSServer(string address, int port)
        {
            System.Console.WriteLine("Changing RCS Address from {0} to {1} and port from {2} to {3}", rcsAddress, address, rcsPort, port);
            rcsAddress = address;
            rcsPort = port;

            if (ClientOpen)
            {

                Close();
            }

            //This should get migrated to Open, and open should get updated with some more checking/error handling code
            try
            {
                Console.WriteLine("Connecting to remote PADE Scope");
                client = new TcpClient(rcsAddress, rcsPort);
                stream = client.GetStream();
                SW = new StreamWriter(stream);
                SR = new StreamReader(stream);
                SW.AutoFlush = true;
                max_timeout = 100;
                ClientOpen = true;
            }
            catch
            {
                return false;

            }
            return true;


        }



 static public bool checkADC(string adc)
        {

			

            char[] delims = { ' ' };
            var splitAdc = adc.Split(delims);
            int nPades = Convert.ToInt32(splitAdc[0]);
           // Console.WriteLine("We Found {0} Pades.", nPades);
            for (int i = 0; i < nPades; i++)
            {
                var padeOffset = 3 * i + 1;
                try
                {

                    var type = splitAdc[padeOffset];
                    if (!((type.Contains("Master") || type.Contains("Slave"))))
                    {

                        Console.WriteLine("Bad ADC message {0}", adc);
                        return false;
                    }
                    else
                    {

                        var controlStatus = splitAdc[padeOffset + 2];
                        byte register = Byte.Parse(controlStatus, NumberStyles.AllowHexSpecifier);
                        if (register != 0xF)
                        {
                            System.Console.WriteLine("Bad Control Register Value!");
                            return false;
                        }
                    }
                }

                catch (Exception e)
                {

                    System.Console.WriteLine("Couldn't parse adc message {0}, {1}", adc, e.ToString());
                    return false;
                }
            }
            return true;
        }

	public static string ADC()
	{
			SW.WriteLine("adc");
			string adcStatus = SR.ReadLine(); 
			return adcStatus; 




	}


        public static bool Arm()
        {
            SW.WriteLine("arm");
            timeout = 0;
            while ((SR.ReadLine() != "arm") && (timeout < max_timeout)) { System.Threading.Thread.Sleep(1); timeout++; }
            if (timeout < max_timeout) { return true; } else { return false; }
        }

        public static bool Disarm()
        {
            SW.WriteLine("disarm");
            timeout = 0;
            while ((SR.ReadLine() != "disarm") && (timeout < max_timeout)) { System.Threading.Thread.Sleep(1); timeout++; }
            if (timeout < max_timeout) { return true; } else { return false; }
        }

        public static bool SoftwareTrig()
        {
            SW.WriteLine("trig");
            timeout = 0;
            while ((SR.ReadLine() != "trig") && (timeout < max_timeout)) { System.Threading.Thread.Sleep(1); timeout++; }
            if (timeout < max_timeout) { return true; } else { return false; }
        }

        public static bool ReadN(int n)
        {
            SW.WriteLine("read " + n.ToString());
            timeout = 0;
            while ((SR.ReadLine().Contains("read") == false) && (timeout < max_timeout)) { System.Threading.Thread.Sleep(1); timeout++; }
            if (timeout < max_timeout) { return true; } else { return false; }

        }

        public static bool ReadAll()
        {
            SW.WriteLine("read all");
            timeout = 0;
            Console.WriteLine();
            
            while ((SR.ReadLine().Contains("read") == false) && (timeout < max_timeout)) { 
                System.Threading.Thread.Sleep(1); 
                timeout++; 
            }
            if (timeout < max_timeout) { return true; } else { return false; }
        }

        public static bool Clear()
        {
            SW.WriteLine("clear");
            timeout = 0;
            while ((SR.ReadLine() != "clear") && (timeout < max_timeout)) { System.Threading.Thread.Sleep(1); timeout++; }
			if (timeout < max_timeout) { return true; } else { return false; }
        }

        public static bool SetMaxTrig(int val)
        {
            SW.WriteLine("maxtrig " + val.ToString());
            timeout = 0;
            return true;
        }


        public static string RawStatus()
        {

            SW.WriteLine("status");
            return SR.ReadLine();



        }

        public static int GetStatus(out string[] status)
        {
            string[] n = new string[10];
            n[0] = "";
            n[1] = "";
            n[2] = "";
            n[3] = "st=";
            n[4] = "ARM=";
            n[5] = "t in mem=";
            n[6] = "err reg=";
            n[7] = "last trig=";
            n[8] = "Ptemp=";
            n[9] = "Stemp=";
            int lines = 0;
            int num_pade = 0;
            string[] tok = new string[1];

            SW.WriteLine("status");

            string t = SR.ReadLine();
            lines++;
	    Console.WriteLine("Status Message: {0}\n", t); 

	    if (!(t.ToUpper().Contains("MASTER"))) { 
	    //we should always have a master line in the status message
	      SW.WriteLine("status");
	      t = SR.ReadLine();
	      Console.WriteLine("Status Message: {0}\n", t); 
	    }
	    	 

            if (t.ToUpper().Contains("MASTER") || t.ToUpper().Contains("SLAVE"))
            {
                string[] delim = new string[1];
                delim[0] = " ";
                tok = t.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                num_pade = Convert.ToInt32(tok[0]);
            }
	    Console.WriteLine("Status {0} Pades found\nToken Length: {1}", num_pade, tok.Length); 


            lines = num_pade;
            string[] s = new string[lines+1];
            if ((tok.Length < 9 * num_pade) || (num_pade == 0))
            {
                for (int i = 0; i < s.Length; i++)
                {
                    s[i] = "error";
                }
                if (num_pade == 0) { s = new string[1]; s[0] = "error, 0 PADE"; }
            }
            else
            {
                int j = 0;
                int k = 0;
                s[k] = "";
                for (int i = 0; i < tok.Length; i++)
                {
                    
                    if (j>0)
                    {
                        s[k] += n[j-9*k]+tok[j] + " ";
                    }
                    j++;
                    if ((j-1) >= (9 * (k + 1))) { k++; s[k] = ""; }
                }
            }
            status = s;
            return lines;
        }



        public static void Close()
        {
            stream.Close();
            client.Close();
            ClientOpen = false;
        }

    }

}
