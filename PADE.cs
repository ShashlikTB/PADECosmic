using System;
using System.Collections.Generic;

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
		
}

