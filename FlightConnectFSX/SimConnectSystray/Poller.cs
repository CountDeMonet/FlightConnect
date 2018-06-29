using System;
using System.Threading;
using System.Diagnostics;
using System.Globalization;

namespace FlightConnectFSX
{
    class Poller
    {
        //private static int countdown = 1000;

        // my main comm thread
        private static Thread pollerThread;

        public Poller()
        {
            CultureInfo ci = new CultureInfo("en-US");

            // start the poller
            pollerThread = new Thread(new ThreadStart(StartListening));
            pollerThread.CurrentCulture = ci;
            pollerThread.Start();
        }

        public static void StartListening()
        {
            while (FlightConnectFSX.runProgram)
            {
                // only send if fsx is active and we have a plane status
                if (DataCollectorFSX.currentPlaneStatus != null && DataCollectorFSX.isFsxActive == true)
                {
                    try
                    {
                        NetworkInterface.sendFMServerMessage("FM_GPS:" + 
                                                             DataCollectorFSX.currentPlaneStatus.GPS_LAT + ":" + DataCollectorFSX.currentPlaneStatus.GPS_LON +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_AIRSPEED_BARBER_POLE + ":" + DataCollectorFSX.currentPlaneStatus.GPS_AIRSPEED_IND + ":" + DataCollectorFSX.currentPlaneStatus.GPS_GROUND_VELOCITY +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_ALT_IND +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_GRND_TRAK + ":" + DataCollectorFSX.currentPlaneStatus.GPS_HEAD_MAG +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.FUEL_LEFT_CAP + ":" + DataCollectorFSX.currentPlaneStatus.FUEL_LEFT_QUANTITY +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.FUEL_RIGHT_CAP + ":" + DataCollectorFSX.currentPlaneStatus.FUEL_RIGHT_QUANTITY +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.VERTICAL_SPEED +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_FLIGHTPLAN_ACTIVE + ":" + DataCollectorFSX.currentPlaneStatus.GPS_FLIGHTPLAN_ETE +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_WAYPOINT_LAT + ":" + DataCollectorFSX.currentPlaneStatus.GPS_WAYPOINT_LON + 
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_WAYPOINT_DIST +":" + DataCollectorFSX.currentPlaneStatus.GPS_WAYPOINT_ALT +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_WAYPOINT_COURSE + ":" + DataCollectorFSX.currentPlaneStatus.GPS_WAYPOINT_ETE + 
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_WAYPOINT_DESIRED + 
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_MAGVAR + ":" + DataCollectorFSX.currentPlaneStatus.GPS_HEAD_IND_MAG +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_WAYPOINT_NAME + ":" + DataCollectorFSX.currentPlaneStatus.PLANE_HEAD_GYRO +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.AUTOPILOT_HEADING);

                        NetworkInterface.sendFGServerMessage("FG_GD:" +
                                                             DataCollectorFSX.currentPlaneStatus.GPS_AIRSPEED_BARBER_POLE + ":" + DataCollectorFSX.currentPlaneStatus.GPS_AIRSPEED_IND +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.GPS_ALT_IND +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.PLANE_HEAD_GYRO +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.AUTOPILOT_HEADING +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.VERTICAL_SPEED +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.DELTA_HEADING_RATE + ":" + DataCollectorFSX.currentPlaneStatus.TURN_COORD_BALL +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.ATT_BANK + ":" + DataCollectorFSX.currentPlaneStatus.ATT_PITCH +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.FUEL_LEFT_CAP + ":" + DataCollectorFSX.currentPlaneStatus.FUEL_LEFT_QUANTITY +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.FUEL_RIGHT_CAP + ":" + DataCollectorFSX.currentPlaneStatus.FUEL_RIGHT_QUANTITY +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.NUM_ENG.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.PROP_1_RPM.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.PROP_2_RPM.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.ENG_1_OIL_TEMP.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.ENG_2_OIL_TEMP.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.ENG_1_OIL_PRES.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.ENG_2_OIL_PRES.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.ENG_1_MAN_PRES.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.ENG_2_MAN_PRES.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.ENG_1_FUEL_FLOW.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.ENG_2_FUEL_FLOW.ToString() + 
                                                             ":" + DataCollectorFSX.currentPlaneStatus.SUCTION_PRESSURE.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.ELECTRICAL_CHARGE.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.CURRENT_TIME.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.AMBIENT_TEMP.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.ADF_RADIAL.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.ADF_CARD.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.NAV1_OBS.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.NAV1_TO_FROM.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.NAV1_HAS_GLIDE_SLOPE.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.NAV1_CDI.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.NAV1_GSI.ToString() );
                        /*
                        if (countdown == 0)
                        {
                            // log the params every so often
                            FlightConnectFSX.Logger.logString( DataCollectorFSX.currentPlaneStatus.NAV1_OBS.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.NAV1_TO_FROM.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.NAV1_HAS_NAV.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.NAV1_HAS_GLIDE_SLOPE.ToString() +
                                                             ":" + DataCollectorFSX.currentPlaneStatus.NAV1_CDI.ToString() + ":" + DataCollectorFSX.currentPlaneStatus.NAV1_GSI.ToString());

                            countdown = 1000;
                        }
                        else
                        {
                            countdown--;
                        }
                        */
                    }
                    catch (Exception ex)
                    {
                        FlightConnectFSX.Logger.logString("Poller: Unable to send client Message: " + ex.ToString());
                    }
                }

                Thread.Sleep(100);
            }
        }

        public void Abort()
        {
            if (pollerThread != null)
            {
                pollerThread.Abort();
            }
        }
    }
}
