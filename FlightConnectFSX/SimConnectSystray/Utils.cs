using System;
using System.Collections.Generic;
using System.Text;

namespace FlightConnectFSX
{
    class Utils
    {
        public static double getToFiveDecimal(double sValue)
        {
            return Math.Round(sValue, 5);
        }
    }
}
