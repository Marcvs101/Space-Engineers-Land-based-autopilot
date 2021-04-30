using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class AutoPilotManager
        {
            enum PilotState
            {
                DISABLED, DRIVING, AVOID
            }

            public Vector3D? Objective { get; set; }

            float powerFactor;
            int precisionFactor;
            int maxScanDistance;
            PilotState state;

            public Vector3D Position { get; set; }
            public Vector3D RelativeVelocity { get; set; }
            private Vector3D RelativeTarget { get; set; }

            public IMyRemoteControl remotePilot;
            private IMyShipController controlReference;
            private WheelController wheelController;
            private CollisionController collisionController;
            private UIManager uIManager;

            // Constructor
            public AutoPilotManager(IMyGridTerminalSystem myGridTerminalSystem, UIManager uIManager, float powerFactor, int precisionFactor, int scanDistance) {
                List<IMyRemoteControl> controllers = new List<IMyRemoteControl>();
                myGridTerminalSystem.GetBlocksOfType(controllers);

                foreach (IMyRemoteControl c in controllers)
                {
                    remotePilot = c;
                    controlReference = remotePilot;
                }
                wheelController = new WheelController(myGridTerminalSystem, controlReference);
                this.powerFactor = powerFactor;
                this.precisionFactor = precisionFactor;
                maxScanDistance = scanDistance;
                state = PilotState.DRIVING;
                this.uIManager = uIManager;
                collisionController = new CollisionController(myGridTerminalSystem, remotePilot.GetValueBool("CollisionAvoidance"));
                uIManager.printOnScreens("service", $"Found {collisionController.cameras.Count} cameras for collision detection.");
            }

            //--------------- Callback commands ---------------

            public Output AddWaypoint(params string[] argument)
            {
                double cmp1, cmp2, cmp3;
                bool success1 = double.TryParse(argument[0], out cmp1);
                bool success2 = double.TryParse(argument[1], out cmp2);
                bool success3 = double.TryParse(argument[2], out cmp3);
                if (success1 && success2 && success3)
                {
                    this.remotePilot.AddWaypoint(new MyWaypointInfo("AutoWaypoint", new Vector3D(cmp1, cmp2, cmp3)));
                    return new Output(true, "[CMD] New destination set");
                }
                else
                {
                    return new Output(false, "[CMD] Error on destination format");
                }
            }

            public Output SpeedLimit(params string[] argument)
            {
                int target;
                bool success = int.TryParse(argument[0], out target);
                if (success)
                {
                    this.remotePilot.SpeedLimit = (float)target / 3.6f;
                    return new Output(true, "[CMD] New speed limit set");
                }
                else
                {
                    return new Output(false, "[CMD] Error on speed limit format");
                }
            }

            public Output Engage(params string[] argument)
            {
                this.remotePilot.SetAutoPilotEnabled(true);
                return new Output(true, "Autopilot on");
            }

            public Output Disengage(params string[] argument)
            {
                this.remotePilot.SetAutoPilotEnabled(false);
                return new Output(true, "Autopilot off");
            }

            //--------------- Pilot management ---------------

            public bool onUpdate()
            {
                if (remotePilot.IsAutoPilotEnabled)
                {
                    if (state == PilotState.DISABLED) state = PilotState.DRIVING;
                    this.Objective = getCurrentObjective();
                    if (this.Objective != null)
                    {
                        double destination = UpdatePosition();
                        remotePilot.HandBrake = false;
                        double tillCollision = -1;

                        if (remotePilot.GetValueBool("CollisionAvoidance") && state == PilotState.DRIVING)
                        {
                            double scanDistance = Math.Min(destination, maxScanDistance);
                            tillCollision = collisionController.checkForCollisions(scanDistance);
                        }
                        if(tillCollision != -1)
                        {// collision detected, reduce speed and steer right
                            state = PilotState.AVOID;

                            Vector3D targetPosition = Vector3D.TransformNormal(this.Position + Vector3D.Right * 10, MatrixD.Transpose(controlReference.WorldMatrix));
                            addObjectiveFirst(targetPosition);
                            uIManager.printOnScreens("service", "[SYS] Avoiding collision");
                            return true;
                        }
                        Cruise(destination, remotePilot.SpeedLimit, RelativeTarget); // Apply Power 

                        if(destination < precisionFactor)
                        {// target reached
                            if (state == PilotState.AVOID) state = PilotState.DRIVING;
                            bool hasAny = RemoveReachedObjective();
                            if(!hasAny)
                            {
                                uIManager.printOnScreens("service", "[SYS] AP destination reached");
                                remotePilot.HandBrake = true;
                                wheelController.ReleaseWheels();
                            }
                        }
                    }
                    else
                    {
                        // No waypoints in list
                        remotePilot.SetAutoPilotEnabled(false);
                        return false;
                    }
                }
                else
                {
                    wheelController.ReleaseWheels();
                    state = PilotState.DISABLED;
                }
                return true;
            }

            private void Cruise(double destination, float speed, Vector3D target)
            {
                // Slow down on turns
                if (Math.Abs(wheelController.Steering) > .1f) speed = speed * ((1f - (Math.Abs(wheelController.Steering)) * .9f) + .1f);
                // Slow down when close to the targed
                if (destination < 50) speed = speed * 0.5f;
                // Slow down when avoiding collision
                if (state == PilotState.AVOID) speed = speed * 0.3f;

                wheelController.SteeringDirection(target, RelativeVelocity, speed);

                // TODO increase power when stuck! (and steer  wheels)
                // Power calculation
                wheelController.moveWheels(RelativeVelocity, speed, powerFactor);
            }

            private double UpdatePosition()
            {
                this.Position = controlReference.GetPosition();
                this.RelativeVelocity = -Vector3D.TransformNormal((controlReference.GetShipVelocities().LinearVelocity), MatrixD.Transpose(controlReference.WorldMatrix));
                this.RelativeTarget = Vector3D.TransformNormal(this.Position - this.Objective.GetValueOrDefault(), MatrixD.Transpose(controlReference.WorldMatrix));
                
                double destination = Math.Sqrt(Math.Pow(RelativeTarget.X, 2) + Math.Pow(RelativeTarget.Z, 2));
                uIManager.printOnScreens("service", "DIST: " + destination + " - " + precisionFactor);
                return destination;
            }

            private Vector3D? getCurrentObjective()
            {
                if(this.Objective == null)
                {
                    List<MyWaypointInfo> route = new List<MyWaypointInfo>();
                    remotePilot.GetWaypointInfo(route);
                    if (route.Count > 0)
                    {
                        MyWaypointInfo nextWaypoint = route.First();
                        return nextWaypoint.Coords;
                    }
                }
                return this.Objective;
            }

            private bool RemoveReachedObjective()
            {
                this.Objective = null;
                List<MyWaypointInfo> route = new List<MyWaypointInfo>();
                remotePilot.GetWaypointInfo(route);
                if (route.Count > 0)
                {
                    MyWaypointInfo nextWaypoint = route.First();
                    route.RemoveAt(0);
                    remotePilot.ClearWaypoints();
                    switch (remotePilot.FlightMode)
                    {
                        case FlightMode.OneWay:
                            break;
                        case FlightMode.Circle:
                            route.Add(nextWaypoint);
                            break;
                        case FlightMode.Patrol: // behave as one way
                            uIManager.printOnScreens("service", "[AP] Patrol not implemented yet!");
                            break;
                    }
                    foreach (MyWaypointInfo wp in route)
                    {
                        remotePilot.AddWaypoint(wp);
                    }
                    //Need to reset the autopilot status to true after clearing the queue
                    remotePilot.SetAutoPilotEnabled(true);
                }
                return route.Count > 0;
            }

            private void addObjectiveFirst(Vector3D position)
            {
                List<MyWaypointInfo> route = new List<MyWaypointInfo>();
                remotePilot.GetWaypointInfo(route);
                route.Insert(0, (new MyWaypointInfo("AutoWaypoint", position)));
                remotePilot.ClearWaypoints();
                foreach (MyWaypointInfo wp in route)
                {
                    remotePilot.AddWaypoint(wp);
                }
                remotePilot.SetAutoPilotEnabled(true);
            }

            //--------------- Info ---------------

            public string DisplayAutopilotInfo()
            {
                string ret = "Autopilot Status";

                if (remotePilot.IsAutoPilotEnabled)
                {
                    ret += "\nAutopilot enabled";

                    if (Math.Abs(this.RelativeVelocity.Z - remotePilot.SpeedLimit) > 2)
                    {
                        ret += "\nAdjusting Speed";
                    }
                    else if (Math.Abs(wheelController.Heading) > 0.2)
                    {
                        ret += "\nAdjusting Course";
                    }
                    else
                    {
                        ret += "\nOn Cruise";
                    }

                    ret += "\nDistance to waypoint: ";
                    ret += Math.Round((this.Objective.GetValueOrDefault() - this.Position).Length()).ToString() + " m";

                    ret += "\nCurrent speed: ";
                    ret += Math.Round(this.RelativeVelocity.Z * 3.6).ToString() + " km/h";

                    ret += "\nETA: ";
                    ret += Math.Round((this.Objective.GetValueOrDefault() - this.Position).Length() / this.RelativeVelocity.Z).ToString() + " s";

                    if(state == PilotState.AVOID)
                        ret += "\nState: Avoiding collision";
                    else
                        ret += "\nState: Normal";
                }
                else
                {
                    ret += "\nAutopilot disabled";
                }
                return ret;
            }

            public string DisplayHUD()
            {
                string ret = "";
                int velZ = (int)Math.Round(this.RelativeVelocity.Z * 3.6);
                int velY = (int)Math.Round(this.RelativeVelocity.Y * 3.6);
                int velX = (int)Math.Round(this.RelativeVelocity.X * 3.6);

                ret += "Speed: " + velZ.ToString() + " km/h";

                if (velY > 3 || velY < -3)
                {
                    ret += " | Vert!";
                }

                if (velX > 2 || velX < -2)
                {
                    ret += " | Lat!";
                }

                ret += "\nAutopilot: ";
                if (remotePilot.IsAutoPilotEnabled)
                {
                    ret += "Enabled";
                }
                else
                {
                    ret += "Disabled";
                }

                return ret;
            }
        }
    }
}
