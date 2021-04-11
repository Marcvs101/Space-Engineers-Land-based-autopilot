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

namespace IngameScript {
    partial class Program : MyGridProgram {

        // ###################
        // Behaviour variables
        // ###################

        float POWER_FACTOR = 20f;
        int PRECISION_FACTOR = 10;

        // ##################################
        // NO MODIFICATIONS BEYOND THIS POINT
        // ##################################

        // Wheels
        private List<IMyMotorSuspension> wheels;
        private Dictionary<bool, List<IMyMotorSuspension>> propulsionDirection;
        private Dictionary<bool, List<IMyMotorSuspension>> steeringDirection;

        // Controller
        private List<IMyRemoteControl> controllers;
        private IMyRemoteControl remotePilot;
        private IMyShipController controlReference;

        // Screens
        private List<IMyTextPanel> screens;

        //private List<string> listaAllerta;

        // Command definitions
        private MyCommandLine commandLine = new MyCommandLine();
        private Dictionary<string, Action> commandList = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        // Definition of the autopilot status structure
        private class AutopilotStatus {
            public Vector3D? Objective { get; set; }
            public double Heading { get; set; }
            public float Power { get; set; }
            public float Steering { get; set; }
            // Constructor
            public AutopilotStatus() {}
        }
        private AutopilotStatus autopilotStatus;

        // Definition of the vehicle status structure
        private class VehicleStatus {
            public Vector3D Position { get; set; }
            public Vector3D RelativeVelocity { get; set; }
            public MyShipVelocities AbsoluteVelocity { get; set; }
            public Vector3D Rotation { get; set; }
            // Constructor
            public VehicleStatus() {}

        }
        private VehicleStatus vehicleStatus;

        // Constructor
        public Program() {
            // Long update interval is used
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            //listaAllerta = new List<string>();

            // Commands declaration

            // Add a waypoint to the list
            commandList["AddWaypoint"] = delegate {
                if (commandLine.Argument(1) != null) {
                    double cmp1, cmp2, cmp3;
                    bool success1 = double.TryParse(commandLine.Argument(1), out cmp1);
                    bool success2 = double.TryParse(commandLine.Argument(2), out cmp2);
                    bool success3 = double.TryParse(commandLine.Argument(3), out cmp3);
                    if (success1 && success2 && success3) {
                        remotePilot.AddWaypoint(new MyWaypointInfo("AutoWaypoint" ,new Vector3D(cmp1, cmp2, cmp3)));
                        Echo("[CMD] New destination set");
                    } else {
                        //listaAllerta.Add("[CMD] Error on destination format");
                        Echo("[CMD] Error on destination format");
                    }
                }
            };

            // Set speed limit
            commandList["SpeedLimit"] = delegate {
                if (commandLine.Argument(1) != null) {
                    int target;
                    bool success = int.TryParse(commandLine.Argument(1), out target);
                    if (success) {
                        remotePilot.SpeedLimit = (float)target/3.6f;
                        Echo("[CMD] New speed limit set");
                    } else {
                        //listaAllerta.Add("[CMD] Error on speed limit format");
                        Echo("[CMD] Error on speed limit format");
                    }
                }
            };

            // Start and stop the autopilotStatus
            commandList["Engage"] = delegate { remotePilot.SetAutoPilotEnabled(true); };
            commandList["Disengage"] = delegate { remotePilot.SetAutoPilotEnabled(false); };

            // System variables init

            // Controllers
            controllers = new List<IMyRemoteControl>();
            GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(controllers);

            foreach (IMyRemoteControl c in controllers) {
                if (c != null) {
                    remotePilot = c;
                    controlReference = remotePilot;
                }
            }

            // Screens
            screens = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(screens);

            foreach (IMyTextPanel t in screens) {
                if (t != null) {
                    if (t.CustomName.ToLower().Contains("autopilot")) {
                        t.WritePublicTitle("Autopilot Status");
                        t.WriteText("Autopilot compiled correctly\nWaiting for system start");
                    } else if (t.CustomName.ToLower().Contains("hud")) {
                        t.WritePublicTitle("HUD");
                        t.WriteText("Autopilot compiled correctly\nWaiting for system start");
                    }
                }
            }

            // Wheels
            wheels = new List<IMyMotorSuspension>();
            GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(wheels);

            steeringDirection = new Dictionary<bool, List<IMyMotorSuspension>>();
            steeringDirection.Add(true, new List<IMyMotorSuspension>());
            steeringDirection.Add(false, new List<IMyMotorSuspension>());

            Vector3D propulsionCenter = new Vector3D();
            foreach (IMyMotorSuspension w in wheels) {
                if (w != null) {
                    propulsionCenter += w.GetPosition();
                }
            }
            propulsionCenter /= wheels.Count;

            foreach (IMyMotorSuspension w in wheels) {
                if (w != null) {
                    Vector3D relPos = Vector3D.TransformNormal(w.GetPosition() - propulsionCenter, MatrixD.Transpose(controlReference.WorldMatrix));
                    if (relPos.Z <= 0) {
                        steeringDirection[true].Add(w);
                    } else {
                        steeringDirection[false].Add(w);
                    }
                }
            }

            propulsionDirection = new Dictionary<bool, List<IMyMotorSuspension>>();
            propulsionDirection.Add(true, new List<IMyMotorSuspension>());
            propulsionDirection.Add(false, new List<IMyMotorSuspension>());

            foreach (IMyMotorSuspension w in wheels) {
                if (w != null) {
                    Vector3D relPos = Vector3D.TransformNormal(w.GetPosition() - propulsionCenter, MatrixD.Transpose(controlReference.WorldMatrix));
                    if (relPos.X <= 0) {
                        propulsionDirection[true].Add(w);
                    } else {
                        propulsionDirection[false].Add(w);
                    }
                }
            }

            // Statuses
            autopilotStatus = new AutopilotStatus();
            //remotePilot.SpeedLimit = 11f;//40km/h currently not in use

            vehicleStatus = new VehicleStatus();
            vehicleStatus.Position = controlReference.GetPosition();
            vehicleStatus.AbsoluteVelocity = controlReference.GetShipVelocities();
            vehicleStatus.RelativeVelocity = -Vector3D.TransformNormal((controlReference.GetShipVelocities().LinearVelocity), MatrixD.Transpose(controlReference.WorldMatrix));

            ReleaseWheels();

            //listaAllerta.Add("[SYS] AutopilotStatus booted up correctly");
            Echo("[SYS] Autopilot booted up correctly");
        }

        public void Save() {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        //Screens

        // HUD screen
        public string DisplayHUD() {
            string ret = "";
            int velZ = (int)Math.Round(vehicleStatus.RelativeVelocity.Z * 3.6);
            int velY = (int)Math.Round(vehicleStatus.RelativeVelocity.Y * 3.6);
            int velX = (int)Math.Round(vehicleStatus.RelativeVelocity.X * 3.6);

            ret += "Speed: " + velZ.ToString() + " km/h";

            if (velY > 3 || velY < -3) {
                ret += " | Vert!";
            }

            if (velX > 2 || velX < -2) {
                ret += " | Lat!";
            }

            ret += "\nAutopilot: ";
            if (remotePilot.IsAutoPilotEnabled) {
                ret += "Enabled";
            } else {
                ret += "Disabled";
            }

            return ret;
        }

        // Status screen
        public string DisplayAutopilotInfo() {
            string ret = "Autopilot Status\n";

            if (remotePilot.IsAutoPilotEnabled) {
                ret += "\nAutopilot enabled\n";

                if (Math.Abs(vehicleStatus.RelativeVelocity.Z - remotePilot.SpeedLimit) > 2) {
                    ret += "\nAdjusting Speed";
                } else if (Math.Abs(autopilotStatus.Heading) > 0.2) {
                    ret += "\nAdjusting Course";
                } else {
                    ret += "\nOn Cruise";
                }

                ret += "\n";

                ret += "\nDistance to waypoint: ";
                ret += Math.Round((autopilotStatus.Objective.GetValueOrDefault() - vehicleStatus.Position).Length()).ToString() + " m";

                ret += "\nCurrent speed: ";
                ret += Math.Round(vehicleStatus.RelativeVelocity.Z * 3.6).ToString() + " km/h";

                ret += "\nETA: ";
                ret += Math.Round((autopilotStatus.Objective.GetValueOrDefault() - vehicleStatus.Position).Length() / vehicleStatus.RelativeVelocity.Z).ToString() + " s";

                ret += "\n";

                ret += "\nLatest warning: NOT DEFINED";
            } else {
                ret += "\nAutopilot disabled";
            }
            return ret;
        }

        // Navigation

        // Release wheel control
        public void Cruise(float speed) {
            // Slow down on turns
            if (Math.Abs(autopilotStatus.Steering) > .1f) speed = speed * ((1f - (Math.Abs(autopilotStatus.Steering)) * .9f) + .1f);

            // Power calculation
            autopilotStatus.Power = (speed - ((float)vehicleStatus.RelativeVelocity.Z)) / POWER_FACTOR;

            // Normalize in in -1:1 range
            if (autopilotStatus.Power > 1) autopilotStatus.Power = 1f;
            else if (autopilotStatus.Power < -1) autopilotStatus.Power = -1f;

            // Move wheels
            foreach (IMyMotorSuspension w in propulsionDirection[true]){w.SetValue("Propulsion override", autopilotStatus.Power);}
            foreach (IMyMotorSuspension w in propulsionDirection[false]){w.SetValue("Propulsion override", -autopilotStatus.Power);}
        }

        // Stering calculations
        public void SteeringDirection(double target) {
            // Normalize in -1:1 range
            if (target > 1) target = 1;
            else if (target < -1) target = -1;

            //Steering calculation
            autopilotStatus.Steering = (float)target;

            float actualTarget = (float)target;

            // No hardsteering at speed
            if (Math.Abs(target) > .1) actualTarget = (float) (target / (((Math.Abs(vehicleStatus.RelativeVelocity.Z) / (double)remotePilot.SpeedLimit) * 5) * .9 + .1));

            // Move wheels
            foreach (IMyMotorSuspension w in steeringDirection[true]){w.SetValue("Steer override", actualTarget);}
            foreach (IMyMotorSuspension w in steeringDirection[false]){w.SetValue("Steer override", -actualTarget);}
        }

        // Release wheel control
        public void ReleaseWheels() {
            foreach (IMyMotorSuspension w in wheels) {
                w.SetValue("Propulsion override", 0.0f);
                w.SetValue("Steer override", 0.0f);
            }
        }

        public void Main(string argument, UpdateType updateSource) {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            // Parse remote or commandline commands
            if (commandLine.TryParse(argument)) {
                Action commandAction;

                var command = commandLine.Argument(0);
                if (command == null) {
                    Echo("[CMD] No command specified");
                } else if (commandList.TryGetValue(command, out commandAction)) {
                    commandAction();
                } else {
                    Echo($"[CMD] Unknown command {command}");
                }
            }

            // Update vehicleStatus
            vehicleStatus.Position = controlReference.GetPosition();
            vehicleStatus.AbsoluteVelocity = controlReference.GetShipVelocities();
            vehicleStatus.RelativeVelocity = -Vector3D.TransformNormal((controlReference.GetShipVelocities().LinearVelocity), MatrixD.Transpose(controlReference.WorldMatrix));

            // Screens
            foreach (IMyTextPanel t in screens) {
                if (t != null) {
                    if (t.CustomName.ToLower().Contains("autopilot")) {
                        t.WriteText(DisplayAutopilotInfo());
                    } else if (t.CustomName.ToLower().Contains("hud")) {
                        t.WriteText(DisplayHUD());
                    }
                }
            }

            // Autopilot
            if (remotePilot.IsAutoPilotEnabled) {
                // If no objective, select one
                if (autopilotStatus.Objective == null) {
                    List<MyWaypointInfo> route = new List<MyWaypointInfo>();
                    remotePilot.GetWaypointInfo(route);

                    if (route.Count > 0) {
                        MyWaypointInfo nextWaypoint = route.First();
                        autopilotStatus.Objective = nextWaypoint.Coords;
                        remotePilot.HandBrake = false;
                    } else {
                        // No waypoints in list
                        remotePilot.SetAutoPilotEnabled(false);
                    }
                    // Speed up clock
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                }
                else {
                    // Objective is selected, navigate to point
                    // Apply Power
                    Cruise(remotePilot.SpeedLimit);

                    // Steering calculations
                    Vector3D relativeTarget = Vector3D.TransformNormal(vehicleStatus.Position - autopilotStatus.Objective.GetValueOrDefault(), MatrixD.Transpose(controlReference.WorldMatrix));
                    autopilotStatus.Heading = (double)(-Math.Atan2(relativeTarget.X, relativeTarget.Z));
                    SteeringDirection(autopilotStatus.Heading);

                    // Distance Calculation
                    Echo("DIST: " + Math.Sqrt(Math.Pow(relativeTarget.X, 2) + Math.Pow(relativeTarget.Z, 2)) + " - " + PRECISION_FACTOR);
                    if (Math.Sqrt(Math.Pow(relativeTarget.X, 2) + Math.Pow(relativeTarget.Z, 2)) < PRECISION_FACTOR) {
                        // Destination has been reached
                        autopilotStatus.Objective = null;
                        //Update waypoint list
                        List<MyWaypointInfo> route = new List<MyWaypointInfo>();
                        remotePilot.GetWaypointInfo(route);

                        if (route.Count > 0) {
                            MyWaypointInfo nextWaypoint = route.First();
                            route.RemoveAt(0);

                            if (remotePilot.FlightMode == FlightMode.OneWay) {
                                // OneWay mode
                                remotePilot.ClearWaypoints();
                                foreach (MyWaypointInfo wp in route) {
                                    remotePilot.AddWaypoint(wp);
                                }
                            } else if (remotePilot.FlightMode == FlightMode.Circle) {
                                // Circle mode
                                route.Add(nextWaypoint);
                                remotePilot.ClearWaypoints();
                                foreach (MyWaypointInfo wp in route) {
                                    remotePilot.AddWaypoint(wp);
                                }
                            } else {
                                // Patrol
                                // Not implemented
                                remotePilot.SetAutoPilotEnabled(false);
                                Echo("[AP] Patrol not implemented yet!");
                                return;
                            }

                            //Need to reset the autopilot status to true after clearing the queue
                            remotePilot.SetAutoPilotEnabled(true);

                        }

                        //listaAllerta.Add("[SYS] AP destination reached");
                        Echo("[SYS] AP destination reached");
                    }
                }
            
            } else if (Runtime.UpdateFrequency != UpdateFrequency.Update100) {
                // Autopilot turning off
                // Slow down clock for better CPU usage
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                autopilotStatus.Objective = null;
                ReleaseWheels();
                //remotePilot.HandBrake = true;
                Echo("[AP] Autopilot Disengaged");
            }
        }
    }
}