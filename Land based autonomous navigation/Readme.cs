﻿/*
 * LAND AUTOPILOT SCRIPT
 * 
 * NEEDED PARTS
 * - A remote control
 * - Wheels
 * 
 * OPTIONAL PARTS
 * - One or more displays with "hud" in the name
 * - One or more displays with "autopilot" in the name
 * - An antenna to recieve remote commands
 * 
 * USAGE
 * - Put the coordinates in the remote control
 * - Select an autopilot mode (note, "Patrol" not yet supported)
 * - IMPORTANT: select an appropriate speed limit
 * - Switch the remote control's autopilot to ON
 * 
 * AVAILABLE PARAMETERS FOR THE SCRIPT
 * - AddWaypoint x y z // Adds a waypoint to the waypoint list
 * - SpeedLimit x // sets the speed limit to x KM/H
 * - Engage // Engages the autopilot (remote controller switch)
 * - Disengage // Disengages the autopilot (remote controller switch)
 * 
 * NOTES
 * For now the autopilot will drive a rover to the waypoints you have set
 * Please keep in mind that in order to avoid damage, you need to manually set a safe path and a safe cruising speed
 * - Collision avoidance in NOT implemented yet!
 * - Terrain detection and avoidance is NOT implemented yet!
 * 
 * CREDIT
 * This script was made by Marcvs101
 * A huge thank you goes to Malware for the MDK-SE and its documentation
*/