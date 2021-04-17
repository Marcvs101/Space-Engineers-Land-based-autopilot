using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        // ###################
        // Behaviour variables
        // ###################

        float POWER_FACTOR = 20f;
        int PRECISION_FACTOR = 10;

        // ##################################
        // NO MODIFICATIONS BEYOND THIS POINT
        // ##################################

        private IMyShipController controlReference;

        // Screens
        private List<IMyTextPanel> screens;

        private UIManager uIManager;
        private AutoPilotManager autopilotManager;
        private WheelController wheelController;

        // Constructor
        public Program()
        {
            // Long update interval is used
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            // Screens
            screens = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(screens);
            uIManager = new UIManager(screens, new List<string> { "autopilot", "hud" }, Echo);
            uIManager.printOnScreens("autopilot", "Autopilot compiled correctly\nWaiting for system to start", "Autopilot Status");
            uIManager.printOnScreens("hud", "Autopilot compiled correctly\nWaiting for system to start", "HUD");

            wheelController = new WheelController(GridTerminalSystem);
            autopilotManager = new AutoPilotManager(GridTerminalSystem, wheelController);

            wheelController.ReleaseWheels();

            uIManager.printOnScreens("service", "[SYS] Autopilot booted up correctly");
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Update10) != 0 || ((updateSource & UpdateType.Update100) != 0))
            {
                if (!uIManager.hasActions())
                {
                    uIManager.registerAction("AddWaypoint", autopilotManager.AddWaypoint);
                    uIManager.registerAction("SpeedLimit", autopilotManager.SpeedLimit);
                    uIManager.registerAction("Engage", autopilotManager.Engage);
                    uIManager.registerAction("Disengage", autopilotManager.Disengage);
                }
                else
                {
                    autopilotManager.onUpdate();

                    uIManager.printOnScreens("autopilot", autopilotManager.DisplayAutopilotInfo());
                    uIManager.printOnScreens("hud", autopilotManager.DisplayHUD());
                }
            }
            else
            {
                if (argument == null)
                {
                    uIManager.printOnScreens("service", "[CMD] No command specified");
                    return;
                }
                if (!uIManager.processAction(argument))
                    uIManager.printOnScreens("service", $"[CMD] Unknown command {argument}");
            }

            // TODO refactor down (move to autopilot man / wheelcontroller / other)
            // Autopilot
            if (autopilotManager.remotePilot.IsAutoPilotEnabled)
            {
                // If no objective, select one
                if (autopilotManager.Objective == null)
                {
                    List<MyWaypointInfo> route = new List<MyWaypointInfo>();
                    autopilotManager.remotePilot.GetWaypointInfo(route);

                    if (route.Count > 0)
                    {
                        MyWaypointInfo nextWaypoint = route.First();
                        autopilotManager.Objective = nextWaypoint.Coords;
                        autopilotManager.remotePilot.HandBrake = false;
                    }
                    else
                    {
                        // No waypoints in list
                        autopilotManager.remotePilot.SetAutoPilotEnabled(false);
                    }
                    // Speed up clock
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                }
                else
                {
                    // Objective is selected, navigate to point
                    // Apply Power
                    autopilotManager.Cruise(POWER_FACTOR);

                    // Steering calculations
                    Vector3D relativeTarget = Vector3D.TransformNormal(autopilotManager.Position - autopilotManager.Objective.GetValueOrDefault(), MatrixD.Transpose(controlReference.WorldMatrix));
                    autopilotManager.Heading = (double)(-Math.Atan2(relativeTarget.X, relativeTarget.Z));
                    autopilotManager.Steering = wheelController.SteeringDirection(autopilotManager.Heading, autopilotManager.remotePilot.SpeedLimit, autopilotManager.RelativeVelocity.Z);

                    // Distance Calculation
                    uIManager.printOnScreens("service", "DIST: " + Math.Sqrt(Math.Pow(relativeTarget.X, 2) + Math.Pow(relativeTarget.Z, 2)) + " - " + PRECISION_FACTOR);
                    if (Math.Sqrt(Math.Pow(relativeTarget.X, 2) + Math.Pow(relativeTarget.Z, 2)) < PRECISION_FACTOR)
                    {
                        // Destination has been reached
                        autopilotManager.Objective = null;
                        //Update waypoint list
                        List<MyWaypointInfo> route = new List<MyWaypointInfo>();
                        autopilotManager.remotePilot.GetWaypointInfo(route);

                        if (route.Count > 0)
                        {
                            MyWaypointInfo nextWaypoint = route.First();
                            route.RemoveAt(0);

                            if (autopilotManager.remotePilot.FlightMode == FlightMode.OneWay)
                            {
                                // OneWay mode
                                autopilotManager.remotePilot.ClearWaypoints();
                                foreach (MyWaypointInfo wp in route)
                                {
                                    autopilotManager.remotePilot.AddWaypoint(wp);
                                }
                            }
                            else if (autopilotManager.remotePilot.FlightMode == FlightMode.Circle)
                            {
                                // Circle mode
                                route.Add(nextWaypoint);
                                autopilotManager.remotePilot.ClearWaypoints();
                                foreach (MyWaypointInfo wp in route)
                                {
                                    autopilotManager.remotePilot.AddWaypoint(wp);
                                }
                            }
                            else
                            {
                                // Patrol
                                // Not implemented
                                autopilotManager.remotePilot.SetAutoPilotEnabled(false);
                                uIManager.printOnScreens("service", "[AP] Patrol not implemented yet!");
                                return;
                            }

                            //Need to reset the autopilot status to true after clearing the queue
                            autopilotManager.remotePilot.SetAutoPilotEnabled(true);

                        }

                        // TODO break after navigation over
                        uIManager.printOnScreens("service", "[SYS] AP destination reached");
                    }
                }

            }
            else if (Runtime.UpdateFrequency != UpdateFrequency.Update100)
            {
                // Autopilot turning off
                // Slow down clock for better CPU usage
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                autopilotManager.Objective = null;
                wheelController.ReleaseWheels();
                //remotePilot.HandBrake = true;  
                uIManager.printOnScreens("service", "[AP] Autopilot Disengaged");
            }
        }
    }
}