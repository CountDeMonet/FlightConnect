# FlightConnect for Microsoft Flight Simulator

This application was developed by Vine Ripe Software to provide instrument and navigation data from FSX to client programs like FlightGauge and FlightMap. 

FlightConnect for FSX is an interface for Flight Simulator X that provides client software with navigation and instrumentation information in real time over UDP. 

FlightConnect for FSX is a software program property of Vine Ripe Consulting LLC. 

Please see http://vineripesoftware.wordpress.com for more information about FlightConnect and to get the latest released version.

################################
Version 2.8.3
################################

What's new: 
-- Resolved crash if FSX SP2 SimConnect is not found. It now alerts and closes down the app
-- Added support for loading FS9 flight plans. Some flight plan software still export that format


################################
Version 2.7.1
################################

What's new: 
-- Added new configuration dialog with IP Address override and Debug Logging Enable option
-- Updated Airport, VOR, NDB request to be real time and not stored
-- Decreased network bandwidth significantly
-- Fixed an issue with the VOR data. This is required for FlightMap 2.7


################################
Version 2.6.14
################################

What's new: 
-- Added support for FlightGauge main gauges - attitude, turn coordinator, etc.
-- Added dual engine gauge information like oil pressure, temp, manifold pressure, etc.
-- Added add ADF and VOR1 params for FlightGauge
-- Improved data connections and support
-- log file now in local app data directory to fix admin rights issue
-- Addressed issue where FlightConnect could not find FSX running on system
-- Support for FlightMap parsing VOR, NDB, and local airport data


################################
Version 1.9.5
################################

What's new: 
-- Improved Performance
-- Full FSX/P3D Flight Plan Support


################################
Version 1.8.2
################################

What's new: 
-- Updated notifications
-- Added Prepar3D support


################################
Version 1.7.0
################################

What's new: 
-- Fixed issues with internationalization.
-- Added waypoint name to parameters.


################################
Version 1.6.1
################################

What's new: 
-- Added magnetic heading indicator for faster updating.
-- Performance enhancments.


################################
Version 1.4.1
################################

What's new: 
-- Added GPS Magnetic Variation.
-- Cleaned up error handling and log writing.


################################
Version 1.3.1
################################

What's new: 
-- Initial public release.
-- Added ground speed
-- Stability Improvements
-- About Box
-- Branding

Requirements for compiling: 
* Microsoft Flight Simulator Developer Tools
* Visual Studio Installer Addon
