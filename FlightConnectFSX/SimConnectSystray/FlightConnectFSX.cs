using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Globalization;

namespace FlightConnectFSX
{
    class FlightConnectFSX
    {
        public static IntPtr ProcessHwnd;
        public static bool runProgram = true;

        public static SystemTray myIcon;
        public static DataCollectorFSX myDataCollector;
        public static NetworkInterface myNetwork;
        public static Poller myPoller;

        public static Logger Logger;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();

            // set my process hwnd
            ProcessHwnd = Process.GetCurrentProcess().MainWindowHandle;

            CultureInfo ci = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = ci;

            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to initialize the Microsoft SimConnect library for FSX SP2 or Acceleration. Make sure FSX has been upgraded to the lastest version.\nSee http://vineripesoftware.wordpress.com for more information.\n\nError: " + ex.Message, "Error loading SimConnect", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            } 
        }

        public static void initialize()
        {
            // create the app logger
            Logger = new Logger();

            // create the system try icon
            myIcon = new SystemTray();
            myIcon.CreateNotifyicon();

            // create the data collector and sender
            myDataCollector = new DataCollectorFSX();

            // init the network stack
            myNetwork = new NetworkInterface();

            // start the poller
            myPoller = new Poller();

            // allow menu clicking to work...
            Application.Run();
        }

        public static void shutDown()
        {
            myIcon.shutdownMenu();
            myPoller.Abort();
            myNetwork.Abort();
            myDataCollector.Abort();
            Application.Exit();
        }
    }
}
