using System;
using System.IO;
using System.Diagnostics;

namespace FlightConnectFSX
{
    public class Logger
    {
        private string _logFileName;
        private StreamWriter _logFile;

        public Logger()
        {
            // this instance log file
            DateTime currentTime = DateTime.Now;
            _logFileName = currentTime.ToString("MM_dd_yy-hh_mm_ss") + "_log.txt";

            String tempLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "VRS_FlightConnect_FSX";
            if (!Directory.Exists(tempLocation))
            {
                Directory.CreateDirectory(tempLocation);
            }

            var fileName = Path.Combine(tempLocation, _logFileName);
            _logFile = new StreamWriter(fileName, true);
        }

        public void logString(string s)
        {
            DateTime currentTime = DateTime.Now;
            Debug.WriteLine(currentTime.ToString("hh:mm:ss.f") + " -- " + s);
            if (Properties.Settings.Default.Enable_Logging == true)
            {
                _logFile.WriteLine(currentTime.ToString("hh:mm:ss.f") + " -- " + s);
                _logFile.Flush();
            }
        }
    }
}
