using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

using Microsoft.FlightSimulator.SimConnect;

namespace FlightConnectFSX
{
    class DataCollectorFSX
    {
        public static bool isFsxActive;
        public static PlaneInfo currentPlaneStatus;
        public static FlightPlan currentFlightPlan;
        public static VorList currentVorList;
        public static NdbList currentNdbList;
        public static AirportList currentAirportList;

        private static int _sleepTime = 5000; // keeps track of how fast to poll the data collector or check fsx connection

        private Thread _keepAliveThread = null;
        private EventWaitHandle _scReady = new EventWaitHandle(false, EventResetMode.AutoReset);
        private volatile bool _killBackgroundThread = false;
        private Thread _backgroundThread = null;

        #region SimConnect specific data structures and handlers

        // User-defined win32 event
        private const int WM_USER_SIMCONNECT = 0x0402;

        // SimConnect object
        private SimConnect simConnect = null;

        enum EventIDs
        {
            FPLACTIVATED
        }

        private enum DEFINITIONS
        {
            AircraftData,
        };

        private enum DATA_REQUESTS
        {
            REQUEST_1,
            SUBSCRIBE_REQ,
            NONSUBSCRIBE_REQ,
        };

        enum EVENTS
        {
            ID0,
        }; 

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private struct FSXData
        {
            // this is how you declare a fixed size string
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String title;
            public double latitude;
            public double longitude;
            public double airspeed_barber_pole;
            public double airspeed_indicated;
            public double ground_velocity;
            public double altitude_indicated;
            public double gps_ground_track;
            public double heading_mag;
            public double fuel_left_cap;
            public double fuel_left_quant;
            public double fuel_right_cap;
            public double fuel_right_quant;
            public double vertical_speed;
            public double gps_flightplan_active;
            public double gps_flightplan_ete;
            public double gps_waypoint_lat;
            public double gps_waypoint_lon;
            public double gps_waypoint_dist;
            public double gps_waypoint_alt;
            public double gps_waypoint_course;
            public double gps_waypoint_ete;
            public double gps_waypoint_dt;
            public double gps_magvar;
            public double head_ind_mag;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public String gps_waypoint_name;
            public double turn_coord_ball;
            public double delta_heading_rate;
            public double attitude_bank_degrees;
            public double attitude_pitch_degrees;
            public double heading_gyro;
            public double num_eng;
            public double prop_1_rpm;
            public double prop_2_rpm;
            public double eng_1_oil_temp;
            public double eng_2_oil_temp;
            public double eng_1_oil_pres;
            public double eng_2_oil_pres;
            public double eng_1_man_pres;
            public double eng_2_man_pres;
            public double eng_1_fuel_flow;
            public double eng_2_fuel_flow;
            public double suction_pressure;
            public double electrical_charge;
            public double autopilot_heading;
            public double current_time;
            public double ambient_temp;
            public double adf_radial;
            public double adf_card;
            public double nav1_obs;
            public double nav1_to_from;
            public double nav1_has_glide_slope;
            public double nav1_cdi;
            public double nav1_gsi;
        };
        #endregion

        // entry point
        public DataCollectorFSX()
        {
            try
            {
                currentPlaneStatus = null;
                currentFlightPlan = new FlightPlan();
                currentVorList = new VorList();
                currentNdbList = new NdbList();
                currentAirportList = new AirportList();
                isFsxActive = false;

                // start the search for FSX running
                _keepAliveThread = new System.Threading.Thread(new System.Threading.ThreadStart(connectionChecker));
                _keepAliveThread.IsBackground = true;
                CultureInfo ci = new CultureInfo("en-US");
                _keepAliveThread.CurrentCulture = ci;
                _keepAliveThread.Start();
            }
            catch (Exception EX)
            {
                FlightConnectFSX.Logger.logString("FSX Data Collector: Error Starting Service: " + EX.Message);
                MessageBox.Show("Error Starting Service. Please make sure this software is installed on a machine that has Flight Simulator X", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }
        }

        // this thread checks the current simConnect status and either calls the 
        // connect simulator method to attempt connection or does nothing if it's connected
        public void connectionChecker()
        {
            while (FlightConnectFSX.runProgram)
            {
                if (!isFsxActive)
                {
                    try
                    {
                        ConnectSimulator();
                    }
                    catch (Exception EX)
                    {
                        MessageBox.Show("Unable to initialize the Microsoft SimConnect library for FSX SP2 or Acceleration. Make sure FSX has been upgraded to the lastest version.\nSee http://vineripesoftware.wordpress.com for more information.\n\nError: " + EX.Message, "Error loading SimConnect", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        FlightConnectFSX.shutDown();
                    }
                }
                else
                {
                    requestData();
                }

                // 10 times a second
                Thread.Sleep(_sleepTime);
            }
        }

        #region SimConnect connection and shutdown procedures
        private void ConnectSimulator()
        {
            if (IsProcessOpen("fsx"))
            {
                if (!isFsxActive)
                {
                    try
                    {
                        simConnect = new SimConnect("Managed Data Request", (IntPtr)0, 0, _scReady, 0);

                        // start the background return thread
                        _backgroundThread = new System.Threading.Thread(new System.Threading.ThreadStart(scMessageThread));
                        _backgroundThread.IsBackground = true;

                        CultureInfo ci = new CultureInfo("en-US");
                        _backgroundThread.CurrentCulture = ci;
                        _backgroundThread.Start();

                        currentFlightPlan = new FlightPlan();
                        currentVorList = new VorList();
                        currentNdbList = new NdbList();
                        currentAirportList = new AirportList();

                        // set up the data request
                        initDataRequest();

                        // we're connected so lets get data quickly
                        _sleepTime = 100;

                        // let everyone know we're ready to get data
                        isFsxActive = true;
                    }
                    catch (COMException cm)
                    {
                        FlightConnectFSX.Logger.logString("FSX Data Collector: Error Connecting: " + cm.Message);
                        // try to free up the com memory
                        GC.Collect();
                        // now call close simulator in case FSX crashed and didn't go cleanly
                        closeSimulator();
                    }
                }
            }
        }

        /// <summary>
        /// Find out if a process is open or not
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private bool firstRun = false;
        private bool IsProcessOpen(string name)
        {
            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (firstRun == true)
                {
                    FlightConnectFSX.Logger.logString("System Process: " + clsProcess.ProcessName);
                }
                if (clsProcess.ProcessName.ToLower().StartsWith(name))
                {
                    // try to force memory collection
                    return true;
                }
            }
            firstRun = false;
            // do it here to. get process can really load up the memory
            GC.Collect();
            return false;
        }

        // handle the message get thread
        private void scMessageThread()
        {
            while (FlightConnectFSX.runProgram)
            {
                if (_killBackgroundThread)
                {
                    _killBackgroundThread = false;
                    return;
                }

                _scReady.WaitOne();
                simConnect.ReceiveMessage();
            }
        }

        private void closeSimulator()
        {
            if (simConnect != null)
            {
                // set the fsx search to 5 second intervals
                _sleepTime = 5000;
                _killBackgroundThread = true;
                simConnect.Dispose();
                simConnect = null;
                isFsxActive = false;
                currentFlightPlan = new FlightPlan();
                currentVorList = new VorList();
                currentNdbList = new NdbList();
                currentAirportList = new AirportList();
                FlightConnectFSX.Logger.logString("FSX Data Collector: Connection closed");
            }
        }
        #endregion

        #region SimConnect related data definitions and event handlers
        private void initDataRequest()
        {
            try
            {
                // listen to connect and quit msgs
                simConnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simConnect_OnRecvOpen);
                simConnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simConnect_OnRecvQuit);

                // get loading of flight plans, vor's, and stuff
                simConnect.OnRecvEventFilename += new SimConnect.RecvEventFilenameEventHandler(simConnect_OnRecvEventFilename);
                //simConnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(simConnect_OnRecvEvent); 
                simConnect.OnRecvVorList += new SimConnect.RecvVorListEventHandler(simConnect_OnRecvVorList);
                simConnect.OnRecvNdbList += new SimConnect.RecvNdbListEventHandler(simConnect_OnRecvNdbList);
                simConnect.OnRecvAirportList += new SimConnect.RecvAirportListEventHandler(simConnect_OnRecvAirportList);

                // listen to exceptions
                simConnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simConnect_OnRecvException);

                // define a data structure
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Title",                            null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Plane Latitude",                   "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Plane Longitude",                  "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Airspeed Barber Pole",             "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Airspeed Indicated",               "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Ground Velocity",                  "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Indicated Altitude",               "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                //simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps Ground True Track",            "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "PLANE HEADING DEGREES TRUE",       "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                //simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Plane Heading Degrees Gyro",       "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Plane Heading Degrees Magnetic",   "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Fuel Left Capacity",               "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Fuel Left Quantity",               "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Fuel Right Capacity",              "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Fuel Right Quantity",              "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Vertical Speed",                   "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps Is Active Flight Plan",        "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps ETE",                          "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps WP Next Lat",                  "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps WP Next Lon",                  "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps WP Distance",                  "meters", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps WP Next Alt",                  "meters", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps Course To Steer",              "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps WP ETE",                       "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps WP Desired Track",             "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Gps Magvar",                       "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "WISKEY COMPASS INDICATION DEGREES","radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "GPS WP NEXT ID",                   null, SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "TURN COORDINATOR BALL",            "position 128", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "DELTA HEADING RATE", 				 "radians per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "ATTITUDE INDICATOR BANK DEGREES",  "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "ATTITUDE INDICATOR PITCH DEGREES", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Plane Heading Degrees Gyro", 		 "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "NUMBER OF ENGINES",                "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Prop1 RPM",                        "rpm", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Prop2 RPM",                        "rpm", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "GENERAL ENG1 OIL TEMPERATURE",     "Rankine", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "GENERAL ENG2 OIL TEMPERATURE",     "Rankine", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "GENERAL ENG1 OIL PRESSURE",        "PSI", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "GENERAL ENG2 OIL PRESSURE",        "PSI", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Eng1 manifold pressure",           "inHg", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Eng2 manifold pressure",           "inHg", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Eng1 fuel flow GPH",               "gallons per hour", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "Eng2 fuel flow GPH",               "gallons per hour", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "SUCTION PRESSURE",                 "inHg", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "ELECTRICAL BATTERY LOAD",          "Amperes", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "AUTOPILOT HEADING LOCK DIR",       "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "LOCAL TIME",                       "Seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "AMBIENT TEMPERATURE",              "Celsius", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "ADF1 Radial",                      "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "ADF CARD",                         "Degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "NAV1 OBS",                         "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "NAV TOFROM:1",                     "enum", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "NAV HAS GLIDE SLOPE:1",            "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "HSI CDI NEEDLE",                   "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simConnect.AddToDataDefinition(DEFINITIONS.AircraftData, "HSI GSI NEEDLE",                   "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                simConnect.RegisterDataDefineStruct<FSXData>(DEFINITIONS.AircraftData);

                // catch a simobject data request
                simConnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(simConnect_OnRecvSimobjectDataBytype);

                // subscribe to facilities -- do not use it's broken
                //simConnect.SubscribeToFacilities(SIMCONNECT_FACILITY_LIST_TYPE.VOR, DATA_REQUESTS.SUBSCRIBE_REQ);
                //simConnect.SubscribeToFacilities(SIMCONNECT_FACILITY_LIST_TYPE.NDB, DATA_REQUESTS.SUBSCRIBE_REQ);
                //simConnect.SubscribeToFacilities(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, DATA_REQUESTS.SUBSCRIBE_REQ);
            }
            catch (COMException ex)
            {
                FlightConnectFSX.Logger.logString("FSX Data Collector: Data Request Exception Thrown: " + ex.Message);
            }
        }

        void simConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            FlightConnectFSX.Logger.logString("FSX Data Collector: Connected to FSX");
            FlightConnectFSX.myIcon.setIcon(true, true);

            sender.SubscribeToSystemEvent(EventIDs.FPLACTIVATED, "FlightPlanActivated");
        }

        // The case where the user closes FSX
        void simConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            FlightConnectFSX.myIcon.setIcon(false, true);
            closeSimulator();
            FlightConnectFSX.Logger.logString("FSX Data Collector: FSX has exited");
        }

        void simConnect_OnRecvEventFilename(SimConnect sender, SIMCONNECT_RECV_EVENT_FILENAME data)
        {
            currentFlightPlan.clearWaypoints();

            FlightConnectFSX.Logger.logString("FSX Data Collector: Loading File: " + data.szFileName);
            
            try
            {
                XmlTextReader reader = new XmlTextReader(data.szFileName);
                while (reader.Read())
                {
                    // load in the waypoints
                    if (reader.ReadToFollowing("ATCWaypoint"))
                    {
                        string fullcoords = "";
                        string waypointID;
                        string waypointType;
                        double waypointLat;
                        double waypointLng;

                        reader.MoveToAttribute(0);
                        waypointID = reader.Value;

                        reader.ReadToFollowing("ATCWaypointType");
                        waypointType = reader.ReadElementContentAsString();

                        reader.ReadToFollowing("WorldPosition");
                        string coords = reader.ReadElementContentAsString();

                        if (coords.Length > 0)
                        {
                            coords = convertCoords(coords);
                            fullcoords += coords + "\n";
                        }

                        string[] splitVal = fullcoords.Split(',');
                        waypointLat = Utils.getToFiveDecimal(Double.Parse(splitVal[0]));
                        waypointLng = Utils.getToFiveDecimal(Double.Parse(splitVal[1]));

                        currentFlightPlan.addWaypoint(waypointID, waypointType, waypointLat, waypointLng);

                        /*
                        FlightConnectFSX.Logger.logString("FSX Data Collector: Found Waypoint Info: ");
                        FlightConnectFSX.Logger.logString("                    Waypoint ID: " + waypointID);
                        FlightConnectFSX.Logger.logString("                    Waypoint Type: " + waypointType);
                        FlightConnectFSX.Logger.logString("                    Waypoint Lat: " + waypointLat.ToString());
                        FlightConnectFSX.Logger.logString("                    Waypoint Lng: " + waypointLng.ToString());
                        */
                    }
               
                }

                FlightConnectFSX.Logger.logString("FSX Data Collector: Flight Plan Loaded");
                NetworkInterface.sendFMServerMessage("FM_FP_LOADED");
            }
            catch (Exception)
            {
                // this could be an FS9 file so try to load it.
                FlightConnectFSX.Logger.logString("FSX Data Collector: FS2004 Flight Plan Detected.");

                try
                {
                    // Read the file and display it line by line.
                    System.IO.StreamReader file = new System.IO.StreamReader(data.szFileName);

                    String line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if (line.Contains("waypoint."))
                        {
                            string[] parts = line.Split(',');
                            string waypointID, waypointType;
                            double waypointLat, waypointLng;

                            waypointID = parts[3].Trim();

                            waypointType = parts[4].Trim();

                            string fullcoords = convertCoordsFS9(parts[5].Trim(), parts[6].Trim());
                            string[] splitVal = fullcoords.Split(',');
                            waypointLat = Utils.getToFiveDecimal(Double.Parse(splitVal[0]));
                            waypointLng = Utils.getToFiveDecimal(Double.Parse(splitVal[1]));

                            currentFlightPlan.addWaypoint(waypointID, waypointType, waypointLat, waypointLng);

                            FlightConnectFSX.Logger.logString("FSX Data Collector: Adding FS9 Waypoint Info: ");
                            FlightConnectFSX.Logger.logString("                    Waypoint ID: " + waypointID);
                            FlightConnectFSX.Logger.logString("                    Waypoint Type: " + waypointType);
                            FlightConnectFSX.Logger.logString("                    Waypoint Lat: " + waypointLat.ToString());
                            FlightConnectFSX.Logger.logString("                    Waypoint Lng: " + waypointLng.ToString());
                        }
                    }

                    file.Close();

                    FlightConnectFSX.Logger.logString("FSX Data Collector: Flight Plan Loaded");
                    NetworkInterface.sendFMServerMessage("FM_FP_LOADED");
                }
                catch (Exception)
                {
                    FlightConnectFSX.Logger.logString("FSX Data Collector: Unable to load flight plan as FSX or FS9.");
                }
            }
        }

        void simConnect_OnRecvAirportList(SimConnect sender, SIMCONNECT_RECV_AIRPORT_LIST data)
        {
            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.SUBSCRIBE_REQ:
                case DATA_REQUESTS.NONSUBSCRIBE_REQ:
                    int sAdded = 0;

                    //FlightConnectFSX.Logger.logString("Airport List:"); 
                    //Dump(data); 
                    //DumpArray(data.rgData); 

                    foreach (Object item in data.rgData)
                    {
                        AirportInfo currentApt = new AirportInfo();

                        foreach (System.Reflection.FieldInfo f in item.GetType().GetFields())
                        {
                            if (!f.FieldType.IsArray)
                            {
                                switch (f.Name)
                                {
                                    case "Icao":
                                        currentApt.APT_ICAO = f.GetValue(item).ToString();
                                        break;
                                    case "Latitude":
                                        currentApt.APT_LAT = Utils.getToFiveDecimal(double.Parse(f.GetValue(item).ToString()));
                                        break;
                                    case "Longitude":
                                        currentApt.APT_LON = Utils.getToFiveDecimal(double.Parse(f.GetValue(item).ToString()));
                                        break;
                                    case "Altitude":
                                        currentApt.APT_ALT = Utils.getToFiveDecimal(double.Parse(f.GetValue(item).ToString()));
                                        break;
                                }
                            }
                        }

                        if (currentApt.APT_ICAO.Length > 0)
                        {
                            if (!currentAirportList.existsAirport(currentApt.APT_ICAO))
                            {
                                currentAirportList.addAirport(currentApt);
                                sAdded++;
                            }
                            else
                            {
                                //FlightConnectFSX.Logger.logString("FSX Data Collector: Found Duplicate Airport and ignoring - " + currentApt.APT_ICAO);
                            }
                            
                        }
                    }
                    FlightConnectFSX.Logger.logString("FSX Data Collector: Added " + sAdded.ToString() + " Airports");

                    // now send the data off the network
                    NetworkInterface.sendLatestAirportList();
                    break;
                default:
                    FlightConnectFSX.Logger.logString("FSX Data Collector: Unknown Airport Request ID: " + data.dwRequestID);
                    break;
            }  
        }

        void simConnect_OnRecvVorList(SimConnect sender, SIMCONNECT_RECV_VOR_LIST data)
        {
            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                 case DATA_REQUESTS.SUBSCRIBE_REQ:
                 case DATA_REQUESTS.NONSUBSCRIBE_REQ:
                    int sAdded = 0;

                    //FlightConnectFSX.Logger.logString("VOR List:"); 
                    //Dump(data); 
                    //DumpArray(data.rgData); 

                    foreach (Object item in data.rgData)
                    {
                        VorInfo currentVor = new VorInfo();

                        foreach (System.Reflection.FieldInfo f in item.GetType().GetFields())
                        {
                            if (!f.FieldType.IsArray)
                            {
                                switch (f.Name)
                                {
                                    case "Flags":
                                        currentVor.VOR_FLAGS = int.Parse(f.GetValue(item).ToString());
                                        break;
                                    case "fFrequency":
                                        currentVor.VOR_FREQ = int.Parse(f.GetValue(item).ToString());
                                        break;
                                    case "Icao":
                                        currentVor.VOR_ICAO = f.GetValue(item).ToString();
                                        break;
                                    case "Latitude":
                                        currentVor.VOR_LAT = Utils.getToFiveDecimal(double.Parse(f.GetValue(item).ToString()));
                                        break;
                                    case "Longitude":
                                        currentVor.VOR_LON = Utils.getToFiveDecimal(double.Parse(f.GetValue(item).ToString()));
                                        break;
                                }
                            }
                        }

                        if (currentVor.VOR_ICAO.Length > 0)
                        {
                            if (!currentVorList.existsVOR(currentVor.VOR_ICAO))
                            {
                                currentVorList.addVOR(currentVor);
                                sAdded++;
                            }
                            else 
                            {
                                //FlightConnectFSX.Logger.logString("FSX Data Collector: Found Duplicate VOR and ignoring - " + currentVor.VOR_ICAO);
                            }
                            
                        }
                    }
                    FlightConnectFSX.Logger.logString("FSX Data Collector: Added " + sAdded.ToString() + " VOR's");

                    // now send the data off the network
                    NetworkInterface.sendLatestVORList();
                    break;
                default:
                    FlightConnectFSX.Logger.logString("FSX Data Collector: Unknown VOR request ID: " + data.dwRequestID);
                    break;
            }
        }

        void simConnect_OnRecvNdbList(SimConnect sender, SIMCONNECT_RECV_NDB_LIST data)
        {
            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.SUBSCRIBE_REQ:
                case DATA_REQUESTS.NONSUBSCRIBE_REQ:
                    int sAdded = 0;

                    //FlightConnectFSX.Logger.logString("NDB List:");
                    //Dump(data);
                    //DumpArray(data.rgData);

                    foreach (Object item in data.rgData)
                    {
                        NdbInfo currentNdb = new NdbInfo();

                        foreach (System.Reflection.FieldInfo f in item.GetType().GetFields())
                        {
                            if (!f.FieldType.IsArray)
                            {
                                switch (f.Name)
                                {
                                    case "fFrequency":
                                        currentNdb.NDB_FREQ = double.Parse(f.GetValue(item).ToString());
                                        break;
                                    case "Icao":
                                        currentNdb.NDB_ICAO = f.GetValue(item).ToString();
                                        break;
                                    case "Latitude":
                                        currentNdb.NDB_LAT = Utils.getToFiveDecimal(double.Parse(f.GetValue(item).ToString()));
                                        break;
                                    case "Longitude":
                                        currentNdb.NDB_LON = Utils.getToFiveDecimal(double.Parse(f.GetValue(item).ToString()));
                                        break;
                                    case "Altitude":
                                        currentNdb.NDB_ALT = Utils.getToFiveDecimal(double.Parse(f.GetValue(item).ToString()));
                                        break;
                                }
                            }
                        }

                        if (currentNdb.NDB_ICAO.Length > 0)
                        {
                            if (!currentNdbList.existsNDB(currentNdb.NDB_ICAO))
                            {
                                currentNdbList.addNDB(currentNdb);
                                sAdded++;
                            }
                            else
                            {
                                //FlightConnectFSX.Logger.logString("FSX Data Collector: Found Duplicate NDB and Ignoring - " + currentNdb.NDB_ICAO);
                            }
                            
                        }
                    }
                    FlightConnectFSX.Logger.logString("FSX Data Collector: Added " + sAdded.ToString() + " NDB's");

                    // now send the data off the network
                    NetworkInterface.sendLatestNDBList();
                    break;
                default:
                    FlightConnectFSX.Logger.logString("FSX Data Collector: Unknown NDB request ID: " + data.dwRequestID);
                    break;
            }
        }

        void simConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            FlightConnectFSX.Logger.logString("FSX Data Collector: Recieve Data Exception received: " + data.dwException);
        }

        void simConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            try
            {
                switch ((DATA_REQUESTS)data.dwRequestID)
                {
                    case DATA_REQUESTS.REQUEST_1:
                        FSXData s1 = (FSXData)data.dwData[0];
                        currentPlaneStatus = new PlaneInfo(s1.latitude, s1.longitude,
                                                           s1.airspeed_barber_pole, s1.airspeed_indicated, s1.ground_velocity,
                                                           s1.altitude_indicated, 
                                                           s1.gps_ground_track, s1.heading_mag,
                                                           s1.fuel_left_cap, s1.fuel_left_quant,
                                                           s1.fuel_right_cap, s1.fuel_right_quant,
                                                           s1.vertical_speed,
                                                           s1.gps_flightplan_active, s1.gps_flightplan_ete, 
                                                           s1.gps_waypoint_lat, s1.gps_waypoint_lon, 
                                                           s1.gps_waypoint_dist, s1.gps_waypoint_alt, 
                                                           s1.gps_waypoint_course, s1.gps_waypoint_ete,
                                                           s1.gps_waypoint_dt, s1.gps_magvar, s1.head_ind_mag, s1.gps_waypoint_name,
                                                           s1.turn_coord_ball, s1.delta_heading_rate, 
                                                           s1.attitude_bank_degrees, s1.attitude_pitch_degrees, s1.heading_gyro,
                                                           s1.num_eng, s1.prop_1_rpm, s1.prop_2_rpm, s1.eng_1_oil_temp, s1.eng_2_oil_temp,
                                                           s1.eng_1_oil_pres, s1.eng_2_oil_pres, s1.eng_1_man_pres, s1.eng_2_man_pres,
                                                           s1.eng_1_fuel_flow, s1.eng_2_fuel_flow, s1.suction_pressure, s1.electrical_charge,
                                                           s1.autopilot_heading, s1.current_time, s1.ambient_temp, s1.adf_radial, s1.adf_card,
                                                           s1.nav1_obs, s1.nav1_to_from, s1.nav1_has_glide_slope, s1.nav1_cdi, s1.nav1_gsi);
                        GC.Collect();
                        break;
                    default:
                        FlightConnectFSX.Logger.logString("FSX Data Collector: Unknown request ID: " + data.dwRequestID);
                        break;
                }
            }
            catch (Exception ex)
            {
                FlightConnectFSX.Logger.logString("FSX Data Collector: Unable to collect current plane status: " + ex.Message);
            }
        }
        #endregion

        public void requestData()
        {
            if (isFsxActive == true)
            {
                try
                {
                    simConnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_1, DEFINITIONS.AircraftData, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                }
                catch (Exception)
                {
                    // fsx is likely closing or not ready. 
                    // lets try to free some memory
                    GC.Collect();
                }
            }
        }

        public void requestAirports()
        {
            currentAirportList.clearAirports();
            simConnect.RequestFacilitiesList(SIMCONNECT_FACILITY_LIST_TYPE.AIRPORT, DATA_REQUESTS.NONSUBSCRIBE_REQ);
        }

        public void requestNDBs()
        {
            currentNdbList.clearNDBs();
            simConnect.RequestFacilitiesList(SIMCONNECT_FACILITY_LIST_TYPE.NDB, DATA_REQUESTS.NONSUBSCRIBE_REQ);
        }

        public void requestVORs()
        {
            currentVorList.clearVORs();
            simConnect.RequestFacilitiesList(SIMCONNECT_FACILITY_LIST_TYPE.VOR, DATA_REQUESTS.NONSUBSCRIBE_REQ);
        }

        private static double ExtractNumbers(string expr)
        {
            CultureInfo c = new CultureInfo("en-us");
            string[] s = System.Text.RegularExpressions.Regex.Split(expr, "[^\\d+(\\.\\d{1,2})?$]");
            return double.Parse(string.Join(null, s), c);
        }

        private static string convertCoords(string coord)
        {
            //"N41° 48' 46.70",E12° 16' 31.10",+000014.00"
            string[] splitted = coord.Split(',');
            string[] first = splitted[0].Split(' ');
            string[] second = splitted[1].Split(' ');
            CultureInfo c = new CultureInfo("en-us");

            string latnorth = "";

            if (first[0].StartsWith("S"))
            {
                latnorth = "-";
            }

            string longeast = "";

            if (second[0].StartsWith("W"))
            {
                longeast = "-";
            }

            double[] lat = new double[3];
            lat[0] = ExtractNumbers(first[0]);
            lat[1] = ExtractNumbers(first[1]);
            lat[2] = ExtractNumbers(first[2]);

            string latitude = (lat[0] + (((lat[1] * 60) + lat[2]) / 3600)).ToString(c);

            double[] lon = new double[3];
            lon[0] = ExtractNumbers(second[0]);
            lon[1] = ExtractNumbers(second[1]);
            lon[2] = ExtractNumbers(second[2]);

            string longitude = (lon[0] + (((lon[1] * 60) + lon[2]) / 3600)).ToString(c);

            return String.Format("{0}{1:0.######},{2}{3:0.######}", latnorth, latitude, longeast, longitude);
        }

        private static string convertCoordsFS9(string lat, string lon)
        {
            // N51* 40.19', W0* 41.92'
            string[] first = lat.Split(' ');
            string[] second = lon.Split(' ');
            CultureInfo c = new CultureInfo("en-us");

            string latnorth = "";

            if (first[0].StartsWith("S"))
            {
                latnorth = "-";
            }

            string longeast = "";

            if (second[0].StartsWith("W"))
            {
                longeast = "-";
            }

            double[] dlat = new double[2];
            dlat[0] = ExtractNumbers(first[0]);
            dlat[1] = ExtractNumbers(first[1]);

            double latitude = (dlat[1]/60) + dlat[0];

            double[] dlon = new double[2];
            dlon[0] = ExtractNumbers(second[0]);
            dlon[1] = ExtractNumbers(second[1]);

            double longitude = (dlon[1] / 60) + dlon[0];

            return String.Format("{0}{1:0.######},{2}{3:0.######}", latnorth, latitude, longeast, longitude); ;
        }

        void Dump(Object item)
        {
            String s = "";
            foreach (System.Reflection.FieldInfo f in item.GetType().GetFields())
            {
                if (!f.FieldType.IsArray)
                {
                    s += "  " + f.Name + ": " + f.GetValue(item);
                }
            }
            FlightConnectFSX.Logger.logString(s);
        }

        void DumpArray(Array rgData)
        {
            foreach (Object item in rgData)
            {
                Dump(item);
            }
        } 

        public void Abort()
        {
            try
            {
                _keepAliveThread.Abort();
                _backgroundThread.Abort();
            }
            catch (Exception)
            {
                // do nothing
            }
        }
    }

    public class PlaneInfo
    {
        public double GPS_LAT;
        public double GPS_LON;
        public double GPS_AIRSPEED_BARBER_POLE;
        public double GPS_AIRSPEED_IND;
        public double GPS_GROUND_VELOCITY;
        public double GPS_ALT_IND;
        public double GPS_GRND_TRAK;
        public double GPS_HEAD_MAG;
        public double FUEL_LEFT_CAP;
        public double FUEL_LEFT_QUANTITY;
        public double FUEL_RIGHT_CAP;
        public double FUEL_RIGHT_QUANTITY;
        public double VERTICAL_SPEED;
        public double GPS_FLIGHTPLAN_ACTIVE;
        public double GPS_FLIGHTPLAN_ETE;
        public double GPS_WAYPOINT_LAT;
        public double GPS_WAYPOINT_LON;
        public double GPS_WAYPOINT_DIST;
        public double GPS_WAYPOINT_ALT;
        public double GPS_WAYPOINT_COURSE;
        public double GPS_WAYPOINT_ETE;
        public double GPS_WAYPOINT_DESIRED;
        public double GPS_MAGVAR;
        public double GPS_HEAD_IND_MAG;
        public String GPS_WAYPOINT_NAME = "";
        public double TURN_COORD_BALL;
        public double DELTA_HEADING_RATE;
        public double ATT_BANK;
        public double ATT_PITCH;
        public double PLANE_HEAD_GYRO;
        public double NUM_ENG;
        public double PROP_1_RPM;
        public double PROP_2_RPM;
        public double ENG_1_OIL_TEMP;
        public double ENG_2_OIL_TEMP;
        public double ENG_1_OIL_PRES;
        public double ENG_2_OIL_PRES;
        public double ENG_1_MAN_PRES;
        public double ENG_2_MAN_PRES;
        public double ENG_1_FUEL_FLOW;
        public double ENG_2_FUEL_FLOW;
        public double SUCTION_PRESSURE;
        public double ELECTRICAL_CHARGE;
        public double AUTOPILOT_HEADING;
        public double CURRENT_TIME;
        public double AMBIENT_TEMP;
        public double ADF_RADIAL;
        public double ADF_CARD;
        public double NAV1_OBS;
        public double NAV1_TO_FROM;
        public double NAV1_HAS_GLIDE_SLOPE;
        public double NAV1_CDI;
        public double NAV1_GSI;

        //s1.prop_1_rpm, s1.prop_2_rpm, s1.eng_1_oil_temp, s1.eng_2_oil_temp,
        //s1.eng_1_oil_pres, s1.eng_2_oil_pres, s1.eng_1_man_pres, s1.eng_2_man_pres,
        //s1.eng_1_fuel_flow, s1.eng_2_fuel_flow, s1.suction_pressure, s1.electrical_charge);
        public PlaneInfo( double lat, double lon,
                          double air_barber, double air_ind, double grnd_velocity,
                          double alt_ind, 
                          double gps_trak, double head_mag, 
                          double left_fuel_cap, double left_fuel_quant, 
                          double right_fuel_cap, double right_fuel_quant,
                          double vertical_speed,
                          double gps_fp_active, double gps_fp_ete,
                          double gps_wp_lat, double gps_wp_lon, 
                          double gps_wp_dist, double gps_wp_alt,
                          double gps_wp_course, double gps_wp_ete, 
                          double gps_wp_dt, double gps_magvar, double gps_head_ind_mag, String gps_waypoint_name,
                          double turn_coord_ball, double delta_heading_rate, 
                          double att_bank, double att_pitch, double plane_head_gyro,
                          double num_eng, double prop_1_rpm, double prop_2_rpm, double eng_1_oil_temp, double eng_2_oil_temp,
                          double eng_1_oil_pres, double eng_2_oil_pres, double eng_1_man_pres, double eng_2_man_pres,
                          double eng_1_fuel_flow, double eng_2_fuel_flow, double suction, double electrical,
                          double autopilot_heading, double current_time, double ambient_temp, double adf_radial, double adf_card,
                          double nav1_obs, double nav1_to_from, double nav1_has_glide, double nav1_cdi, double nav1_gsi)
        {
            GPS_LAT = Utils.getToFiveDecimal(lat);
            GPS_LON = Utils.getToFiveDecimal(lon);
            GPS_AIRSPEED_BARBER_POLE = Utils.getToFiveDecimal(air_barber);
            GPS_AIRSPEED_IND = Utils.getToFiveDecimal(air_ind);
            GPS_ALT_IND = Utils.getToFiveDecimal(alt_ind);
            GPS_GROUND_VELOCITY = Utils.getToFiveDecimal(grnd_velocity);
            GPS_GRND_TRAK = Utils.getToFiveDecimal(gps_trak);
            GPS_HEAD_MAG = Utils.getToFiveDecimal(head_mag);
            FUEL_LEFT_CAP = Utils.getToFiveDecimal(left_fuel_cap);
            FUEL_LEFT_QUANTITY = Utils.getToFiveDecimal(left_fuel_quant);
            FUEL_RIGHT_CAP = Utils.getToFiveDecimal(right_fuel_cap);
            FUEL_RIGHT_QUANTITY = Utils.getToFiveDecimal(right_fuel_quant);
            VERTICAL_SPEED = Utils.getToFiveDecimal(vertical_speed);
            GPS_FLIGHTPLAN_ACTIVE = Utils.getToFiveDecimal(gps_fp_active);
            GPS_FLIGHTPLAN_ETE = Utils.getToFiveDecimal(gps_fp_ete);
            GPS_WAYPOINT_LAT = Utils.getToFiveDecimal(gps_wp_lat);
            GPS_WAYPOINT_LON = Utils.getToFiveDecimal(gps_wp_lon);
            GPS_WAYPOINT_DIST = Utils.getToFiveDecimal(gps_wp_dist);
            GPS_WAYPOINT_ALT = Utils.getToFiveDecimal(gps_wp_alt);
            GPS_WAYPOINT_COURSE = Utils.getToFiveDecimal(gps_wp_course);
            GPS_WAYPOINT_ETE = Utils.getToFiveDecimal(gps_wp_ete);
            GPS_WAYPOINT_DESIRED = Utils.getToFiveDecimal(gps_wp_dt);
            GPS_MAGVAR = Utils.getToFiveDecimal(gps_magvar);
            GPS_HEAD_IND_MAG = Utils.getToFiveDecimal(gps_head_ind_mag);
            if( !gps_waypoint_name.Trim().Equals(String.Empty) ){
                GPS_WAYPOINT_NAME = gps_waypoint_name;
            }
            TURN_COORD_BALL = Utils.getToFiveDecimal(turn_coord_ball);
            DELTA_HEADING_RATE = Utils.getToFiveDecimal(delta_heading_rate);
            ATT_BANK = Utils.getToFiveDecimal(att_bank);
            ATT_PITCH = Utils.getToFiveDecimal(att_pitch);
            PLANE_HEAD_GYRO = Utils.getToFiveDecimal(plane_head_gyro);
            NUM_ENG = Utils.getToFiveDecimal(num_eng);
            PROP_1_RPM = Utils.getToFiveDecimal(prop_1_rpm);
            PROP_2_RPM = Utils.getToFiveDecimal(prop_2_rpm);
            ENG_1_OIL_TEMP = Utils.getToFiveDecimal(eng_1_oil_temp);
            ENG_2_OIL_TEMP = Utils.getToFiveDecimal(eng_2_oil_temp);
            ENG_1_OIL_PRES = Utils.getToFiveDecimal(eng_1_oil_pres);
            ENG_2_OIL_PRES = Utils.getToFiveDecimal(eng_2_oil_pres);
            ENG_1_MAN_PRES = Utils.getToFiveDecimal(eng_1_man_pres);
            ENG_2_MAN_PRES = Utils.getToFiveDecimal(eng_2_man_pres);
            ENG_1_FUEL_FLOW = Utils.getToFiveDecimal(eng_1_fuel_flow);
            ENG_2_FUEL_FLOW = Utils.getToFiveDecimal(eng_2_fuel_flow);
            SUCTION_PRESSURE = Utils.getToFiveDecimal(suction);
            ELECTRICAL_CHARGE = Utils.getToFiveDecimal(electrical);
            AUTOPILOT_HEADING = Utils.getToFiveDecimal(autopilot_heading);
            CURRENT_TIME = current_time;
            AMBIENT_TEMP = Utils.getToFiveDecimal(ambient_temp);
            ADF_RADIAL = Utils.getToFiveDecimal(adf_radial);
            ADF_CARD = Utils.getToFiveDecimal(adf_card);
            NAV1_OBS = Utils.getToFiveDecimal(nav1_obs);
            NAV1_TO_FROM = Utils.getToFiveDecimal(nav1_to_from);
            NAV1_HAS_GLIDE_SLOPE = Utils.getToFiveDecimal(nav1_has_glide);
            NAV1_CDI = Utils.getToFiveDecimal(nav1_cdi);
            NAV1_GSI = Utils.getToFiveDecimal(nav1_gsi);
        }
    }

    public class AirportList
    {
        private int maxAirportListSize = 500;
        private List<AirportInfo> AIRPORT_ITEMS;

        public AirportList()
        {
            AIRPORT_ITEMS = new List<AirportInfo>();
        }

        public void addAirport(string apt_icao, double apt_lat, double apt_lon, double apt_alt)
        {
            if ((AIRPORT_ITEMS.Count+1) >= maxAirportListSize)
            {
                AIRPORT_ITEMS.RemoveAt(0);
            }

            AirportInfo newVorItem = new AirportInfo(apt_icao, apt_lat, apt_lon, apt_alt);
            AIRPORT_ITEMS.Add(newVorItem);
        }

        public void addAirport(AirportInfo newVorItem)
        {
            if ((AIRPORT_ITEMS.Count+1) >= maxAirportListSize)
            {
                AIRPORT_ITEMS.RemoveAt(0);
            }
            AIRPORT_ITEMS.Add(newVorItem);
        }

        public void clearAirports()
        {
            AIRPORT_ITEMS.Clear();
        }

        public List<AirportInfo> getAirports()
        {
            return AIRPORT_ITEMS;
        }

        public Boolean existsAirport(String APT_ICAO)
        {
            return AIRPORT_ITEMS.Exists((delegate(AirportInfo x) { return (string.Equals(x.APT_ICAO, APT_ICAO)) ? true : false; }));
        }
    }

    public class VorList
    {
        private int maxVorListSize = 500;
        private List<VorInfo> VOR_ITEMS;

        public VorList()
        {
            VOR_ITEMS = new List<VorInfo>();
        }

        public void addVOR(int vor_flags, int vor_freq, string vor_icao, double vor_lat, double vor_lon)
        {
            // assumes 
            if ((VOR_ITEMS.Count+1) >= maxVorListSize)
            {
                VOR_ITEMS.RemoveAt(0);
            }

            VorInfo newVorItem = new VorInfo(vor_flags, vor_freq, vor_icao, vor_lat, vor_lon);
            VOR_ITEMS.Add(newVorItem);
        }

        public void addVOR(VorInfo newVorItem)
        {
            if ((VOR_ITEMS.Count + 1) >= maxVorListSize)
            {
                VOR_ITEMS.RemoveAt(0);
            }
            VOR_ITEMS.Add(newVorItem);
        }

        public void clearVORs()
        {
            VOR_ITEMS.Clear();
        }

        public List<VorInfo> getVORs()
        {
            return VOR_ITEMS;
        }

        public Boolean existsVOR(String VOR_ICAO)
        {
            return VOR_ITEMS.Exists( ( delegate(VorInfo x) { return (string.Equals(x.VOR_ICAO, VOR_ICAO)) ? true : false; } ) );
        }
    }

    public class NdbList
    {
        private int maxNDBListSize = 500;
        private List<NdbInfo> NDB_ITEMS;

        public NdbList()
        {
            NDB_ITEMS = new List<NdbInfo>();
        }

        public void addNDB(double ndb_freq, string ndb_icao, double ndb_lat, double ndb_lon, double ndb_alt)
        {
            // assumes 
            if ((NDB_ITEMS.Count+1) >= maxNDBListSize)
            {
                NDB_ITEMS.RemoveAt(0);
            }

            NdbInfo newNdbItem = new NdbInfo(ndb_freq, ndb_icao, ndb_lat, ndb_lon, ndb_alt);
            NDB_ITEMS.Add(newNdbItem);
        }

        public void addNDB(NdbInfo newNdbItem)
        {
            if ((NDB_ITEMS.Count+1) >= maxNDBListSize)
            {
                NDB_ITEMS.RemoveAt(0);
            }
            NDB_ITEMS.Add(newNdbItem);
        }

        public void clearNDBs()
        {
            NDB_ITEMS.Clear();
        }

        public List<NdbInfo> getNDBs()
        {
            return NDB_ITEMS;
        }

        public Boolean existsNDB(String NDB_ICAO)
        {
            return NDB_ITEMS.Exists((delegate(NdbInfo x) { return (string.Equals(x.NDB_ICAO, NDB_ICAO)) ? true : false; }));
        }
    }

    public class FlightPlan
    {
        private List<Waypoint> FP_WAYPOINTS;

        public FlightPlan()
        {
            FP_WAYPOINTS = new List<Waypoint>();
        }

        public void addWaypoint(String wp_id, String wp_type, double wp_lat, double wp_lng)
        {
            Waypoint newWaypoint = new Waypoint(wp_id, wp_type, wp_lat, wp_lng);
            FP_WAYPOINTS.Add(newWaypoint);
        }

        public void clearWaypoints()
        {
            FP_WAYPOINTS.Clear();
        }

        public List<Waypoint> getWaypoints()
        {
            return FP_WAYPOINTS;
        }
    }

    public class AirportInfo
    {
        public String APT_ICAO;
        public double APT_LAT;
        public double APT_LON;
        public double APT_ALT;

        public AirportInfo() { }

        public AirportInfo(string apt_icao, double apt_lat, double apt_lon, double apt_alt)
        {
            APT_ICAO = apt_icao;
            APT_LAT = apt_lat;
            APT_LON = apt_lon;
            APT_ALT = apt_alt;
        }
    }

    public class VorInfo
    {
        public int VOR_FLAGS;
        public int VOR_FREQ;
        public String VOR_ICAO;
        public double VOR_LAT;
        public double VOR_LON;

        //private int HAS_NAV_SIGNAL  = 0x00000001;
        //private int HAS_LOCALIZER   = 0x00000002;
        //private int HAS_GLIDE_SLOPE = 0x00000004;
        private int HAS_DME         = 0x00000008;

        public VorInfo() { }

        public VorInfo(int vor_flags, int vor_freq, string vor_icao, double vor_lat, double vor_lon)
        {
            VOR_FLAGS = vor_flags;
            VOR_FREQ = vor_freq;
            VOR_ICAO = vor_icao;
            VOR_LAT = Utils.getToFiveDecimal(vor_lat);
            VOR_LON = Utils.getToFiveDecimal(vor_lon);
        }

        private Boolean hasFlag(int constant)
        {
            return (VOR_FLAGS & constant) != 0;
        }

        public Boolean hasDME()
        {
            return hasFlag(HAS_DME);
        }
    }

    public class NdbInfo
    {
        public double NDB_FREQ;
        public String NDB_ICAO;
        public double NDB_LAT;
        public double NDB_LON;
        public double NDB_ALT;

        public NdbInfo() { }

        public NdbInfo(double ndb_freq, string ndb_icao, double ndb_lat, double ndb_lon, double ndb_alt)
        {
            NDB_FREQ = ndb_freq;
            NDB_ICAO = ndb_icao;
            NDB_LAT = ndb_lat;
            NDB_LON = Utils.getToFiveDecimal(ndb_lon);
            NDB_ALT = Utils.getToFiveDecimal(ndb_alt);
        }
    }

    public class Waypoint
    {
        public string WAYPOINT_ID;
        public string WAYPOINT_TYPE;
        public double WAYPOINT_LAT;
        public double WAYPOINT_LNG;

        public Waypoint(String wp_id, String wp_type, double wp_lat, double wp_lng)
        {
            WAYPOINT_ID = wp_id;
            WAYPOINT_TYPE = wp_type;
            WAYPOINT_LAT = Utils.getToFiveDecimal(wp_lat);
            WAYPOINT_LNG = Utils.getToFiveDecimal(wp_lng);
        }
    }
}
