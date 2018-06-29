using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FlightConnectFSX
{
    class NetworkInterface
    {
        private static String clientFlightMapIP = null;
        private static String clientFlightGaugeIP = null;
        private static String clientFlightMapLiteIP = null;

        // our port numbers
        private const int controlPort = 8004;

        private static String serverIP;
        private static UdpClient controlListener;
        private static IPEndPoint controlEndpoint;

        // my main comm thread
        private static Thread networkThread;

        public NetworkInterface()
        {
            CultureInfo ci = new CultureInfo("en-US");
            try
            {
                if (Properties.Settings.Default.IP_Override == String.Empty)
                {
                    serverIP = findServerIP();
                }
                else
                {
                    serverIP = Properties.Settings.Default.IP_Override;
                }
                FlightConnectFSX.Logger.logString("UDP Server: Initialize network interface with IP: " + serverIP);

                //Create the UdpClient server
                controlEndpoint = new IPEndPoint(IPAddress.Any, controlPort);
                controlListener = new UdpClient(controlEndpoint);

                // start up the network thread
                networkThread = new Thread(new ThreadStart(StartListening));
                networkThread.CurrentCulture = ci;
                networkThread.Start();
            }
            catch (Exception e)
            {
                FlightConnectFSX.Logger.logString("UDP Server: Error Starting Service: " + e.ToString());
                MessageBox.Show("Error Starting Service.\nMake sure only one instance of the service is running at any given time and that port 8004 is open on this machine.", "Network Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }
        }

        public static void resetClient()
        {
            clientFlightMapIP = null;
            clientFlightGaugeIP = null;
            clientFlightMapLiteIP = null;
        }

        public static void StartListening()
        {
            FlightConnectFSX.Logger.logString("UDP Server: Listener Started...");

            while (FlightConnectFSX.runProgram)
            {
                try
                {
                    byte[] controlByteArray = controlListener.Receive(ref controlEndpoint);
                    string controlData = Encoding.ASCII.GetString(controlByteArray, 0, controlByteArray.Length);
                    string[] controlArray = controlData.Split(':');

                    int controlParam = int.Parse(controlArray[0]);
                    string controlMessage = "";
                    if (controlArray.Length > 1)
                    {
                        controlMessage = controlArray[1];
                    }

                    switch (controlParam)
                    {
                        case 0:
                            FlightConnectFSX.Logger.logString("UDP Server: Received a FlightMap connect request from " + controlMessage + ", sending response");

                            if (controlMessage != "0.0.0.0")
                            {
                                clientFlightMapIP = controlMessage;
                            }
                            sendMessage("FM_CONNECT:" + SystemTray.VERSION_NUMBER);
                            break;
                        case 1:
                            FlightConnectFSX.Logger.logString("UDP Server: Received a discovery request for server info sending response");

                            // broadcast response
                            if (Properties.Settings.Default.IP_Override == String.Empty)
                            {
                                string tmpIp = findServerIP();
                                if (serverIP != tmpIp)
                                {
                                    serverIP = tmpIp;
                                    FlightConnectFSX.myIcon.setIcon(DataCollectorFSX.isFsxActive, true);
                                }
                            }
                            else
                            {
                                serverIP = Properties.Settings.Default.IP_Override;
                            }
                            sendMessage("DISCOVER_FM_RESPONSE:" + serverIP);
                            break;
                        case 2: // get loaded FlightPlan
                            FlightConnectFSX.Logger.logString("UDP Server: Get Current FlightPlan");

                            if (DataCollectorFSX.currentFlightPlan.getWaypoints().Count > 0)
                            {
                                string activeFlightPlan = "ACTIVE_FP";
                                foreach (Waypoint waypoint in DataCollectorFSX.currentFlightPlan.getWaypoints())
                                {
                                    activeFlightPlan +=  ":" + waypoint.WAYPOINT_ID + ";" + waypoint.WAYPOINT_TYPE + ";" + waypoint.WAYPOINT_LAT + ";" + waypoint.WAYPOINT_LNG;
                                }
                                sendMessage(activeFlightPlan);
                            }
                            else
                            {
                                sendMessage("NO_FP_LOADED");
                            }
                            break;
                        case 3:
                            FlightConnectFSX.Logger.logString("UDP Server: Received a FlightGauge connect request from " + controlMessage + ", sending response");
                            if (controlMessage != "0.0.0.0")
                            {
                                clientFlightGaugeIP = controlMessage;
                            }
                            sendMessage("FG_CONNECT:" + SystemTray.VERSION_NUMBER);
                            break;
                        case 4:
                            FlightConnectFSX.Logger.logString("UDP Server: Received a FlightMap Lite connect request from " + controlMessage + ", sending response");
                            if (controlMessage != "0.0.0.0")
                            {
                                clientFlightMapLiteIP = controlMessage;
                            }
                            sendMessage("FML_CONNECT:" + SystemTray.VERSION_NUMBER);
                            break;
                        case 5:
                            FlightConnectFSX.myDataCollector.requestAirports();
                            break;
                        case 6:
                            FlightConnectFSX.myDataCollector.requestVORs();
                            break;
                        case 7:
                            FlightConnectFSX.myDataCollector.requestNDBs();
                            break;
                    }
                }
                catch (Exception e)
                {
                    FlightConnectFSX.Logger.logString("UDP Server: exception: " + e.Message);
                }
            }
            return;
        }

        public static void sendLatestAirportList()
        {
            FlightConnectFSX.Logger.logString("UDP Server: Get Current Airport List: " + DataCollectorFSX.currentAirportList.getAirports().Count);

            if (DataCollectorFSX.currentAirportList.getAirports().Count > 0)
            {
                string activeAirportList = "AIRPORTS";
                foreach (AirportInfo airport in DataCollectorFSX.currentAirportList.getAirports())
                {
                    activeAirportList += ":" + airport.APT_ICAO + ";" + airport.APT_LAT + ";" + airport.APT_LON;
                }
                //FlightConnectFSX.Logger.logString("UDP Server: Current Airport List: " + activeAirportList);
                sendMessage(activeAirportList);
            }
            else
            {
                sendMessage("NO_APTS_LOADED");
            }
        }

        public static void sendLatestVORList()
        {
            FlightConnectFSX.Logger.logString("UDP Server: Get Current VOR List: " + DataCollectorFSX.currentVorList.getVORs().Count);

            if (DataCollectorFSX.currentVorList.getVORs().Count > 0)
            {
                string activeVORList = "VORS";
                foreach (VorInfo vor in DataCollectorFSX.currentVorList.getVORs())
                {
                    activeVORList += ":" + vor.VOR_ICAO + ";" + vor.VOR_FREQ + ";" + vor.VOR_LAT + ";" + vor.VOR_LON + ";" + vor.hasDME();
                }
                //FlightConnectFSX.Logger.logString("UDP Server: Current VOR List: " + activeVORList);
                sendMessage(activeVORList);
            }
            else
            {
                sendMessage("NO_VORS_LOADED");
            }
        }

        public static void sendLatestNDBList()
        {
            FlightConnectFSX.Logger.logString("UDP Server: Get Current NDB List: " + DataCollectorFSX.currentNdbList.getNDBs().Count); 
            if (DataCollectorFSX.currentNdbList.getNDBs().Count > 0)
            {
                string activeNDBList = "NDBS";
                foreach (NdbInfo ndb in DataCollectorFSX.currentNdbList.getNDBs())
                {
                    activeNDBList += ":" + ndb.NDB_ICAO + ";" + ndb.NDB_FREQ + ";" + ndb.NDB_LAT + ";" + ndb.NDB_LON;
                }
                //FlightConnectFSX.Logger.logString("UDP Server: Current NDB List: " + activeNDBList);
                sendMessage(activeNDBList);
            }
            else
            {
                sendMessage("NO_NDBS_LOADED");
            }
        }

        // send a message back to the last client
        private static void sendMessage(String message)
        {
            byte[] data = new byte[1024];
            data = Encoding.ASCII.GetBytes(message);
            controlListener.Send(data, data.Length, controlEndpoint);
        }

        public static void sendFMServerMessage(String message)
        {
            // only try to send data if we have a client id.
            if (clientFlightMapIP != "0.0.0.0" && clientFlightMapIP != null)
            {
                UdpClient udpClient = new UdpClient();

                Byte[] sendBytes = Encoding.ASCII.GetBytes(message);
                try
                {
                    //Debug.WriteLine("UDP Server - Sending '" + clientIP + "' Message: " + message);
                    udpClient.Send(sendBytes, sendBytes.Length, clientFlightMapIP, 8005);
                }
                catch (Exception e)
                {
                    FlightConnectFSX.Logger.logString("UDP Server: Send FM Server Message Error: " + e.ToString());
                }
            }
        }

        public static void sendFGServerMessage(String message)
        {
            // only try to send data if we have a client id.
            if (clientFlightGaugeIP != "0.0.0.0" && clientFlightGaugeIP != null)
            {
                UdpClient udpClient = new UdpClient();

                Byte[] sendBytes = Encoding.ASCII.GetBytes(message);
                try
                {
                    //Debug.WriteLine("UDP Server - Sending '" + clientIP + "' Message: " + message);
                    udpClient.Send(sendBytes, sendBytes.Length, clientFlightGaugeIP, 8006);
                }
                catch (Exception e)
                {
                    FlightConnectFSX.Logger.logString("UDP Server: Send FG Server Message Error: " + e.ToString());
                }
            }
        }

        public static void sendFMLServerMessage(String message)
        {
            // only try to send data if we have a client id.
            if (clientFlightMapLiteIP != "0.0.0.0" && clientFlightMapLiteIP != null)
            {
                UdpClient udpClient = new UdpClient();

                Byte[] sendBytes = Encoding.ASCII.GetBytes(message);
                try
                {
                    //Debug.WriteLine("UDP Server - Sending '" + clientIP + "' Message: " + message);
                    udpClient.Send(sendBytes, sendBytes.Length, clientFlightMapLiteIP, 8006);
                }
                catch (Exception e)
                {
                    FlightConnectFSX.Logger.logString("UDP Server: Send FML Server Message Error: " + e.ToString());
                }
            }
        }

        // guess at the serve ip for discovery
        public static string findServerIP(Boolean addLog = true)
        {
            IPHostEntry host;
            string localIP = String.Empty;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (addLog)
                {
                    FlightConnectFSX.Logger.logString("UDP Server: Detected Network Family: " + ip.AddressFamily.ToString() + " for ip address: " + ip.ToString());
                }

                if (ip.AddressFamily.ToString() == "InterNetwork")
                {
                    // only use the first one
                    if (localIP == String.Empty)
                    {
                        localIP = ip.ToString();
                        if (addLog)
                        {
                            FlightConnectFSX.Logger.logString("UDP Server: Selecting InterNetwork Server Ip Address for FlightConnect: " + ip.ToString());
                        }
                    }
                }
            }
            return localIP;
        }

        // close down the network server
        public void Abort()
        {
            if (networkThread != null)
            {
                controlListener.Close();
                networkThread.Abort();
            }
        }
    }
}
