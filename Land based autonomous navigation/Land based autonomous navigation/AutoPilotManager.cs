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
            public Vector3D? Objective { get; set; }
            public double Heading { get; set; }
            public float Power { get; set; }
            public float Steering { get; set; }

            public Vector3D Position { get; set; }
            public Vector3D RelativeVelocity { get; set; }
            public MyShipVelocities AbsoluteVelocity { get; set; }
            public Vector3D Rotation { get; set; }

            public IMyRemoteControl remotePilot;
            private IMyShipController controlReference;
            private WheelController wheelController;

            // Constructor
            public AutoPilotManager(IMyGridTerminalSystem myGridTerminalSystem, WheelController wheelController) {
                List<IMyRemoteControl> controllers = new List<IMyRemoteControl>();
                myGridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(controllers);

                foreach (IMyRemoteControl c in controllers)
                {
                    remotePilot = c;
                    controlReference = remotePilot;
                }
                Position = controlReference.GetPosition();
                AbsoluteVelocity = controlReference.GetShipVelocities();
                RelativeVelocity = -Vector3D.TransformNormal((controlReference.GetShipVelocities().LinearVelocity), MatrixD.Transpose(controlReference.WorldMatrix));
                this.wheelController = wheelController;
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

            public void Cruise(float powerFactor)
            {
                float speed = remotePilot.SpeedLimit;
                // Slow down on turns
                if (Math.Abs(this.Steering) > .1f) speed = speed * ((1f - (Math.Abs(this.Steering)) * .9f) + .1f);

                // Power calculation
                this.Power = (speed - ((float)this.RelativeVelocity.Z)) / powerFactor;

                // Normalize in in -1:1 range
                if (this.Power > 1) this.Power = 1f;
                else if (this.Power < -1) this.Power = -1f;

                wheelController.moveWheels(this.Power);
            }

            public void onUpdate()
            {
                Position = controlReference.GetPosition();
                AbsoluteVelocity = controlReference.GetShipVelocities();
                RelativeVelocity = -Vector3D.TransformNormal((controlReference.GetShipVelocities().LinearVelocity), MatrixD.Transpose(controlReference.WorldMatrix));
            }

            //--------------- Info ---------------

            public string DisplayAutopilotInfo()
            {
                string ret = "Autopilot Status\n";

                if (remotePilot.IsAutoPilotEnabled)
                {
                    ret += "\nAutopilot enabled\n";

                    if (Math.Abs(this.RelativeVelocity.Z - remotePilot.SpeedLimit) > 2)
                    {
                        ret += "\nAdjusting Speed";
                    }
                    else if (Math.Abs(this.Heading) > 0.2)
                    {
                        ret += "\nAdjusting Course";
                    }
                    else
                    {
                        ret += "\nOn Cruise";
                    }

                    ret += "\n";

                    ret += "\nDistance to waypoint: ";
                    ret += Math.Round((this.Objective.GetValueOrDefault() - this.Position).Length()).ToString() + " m";

                    ret += "\nCurrent speed: ";
                    ret += Math.Round(this.RelativeVelocity.Z * 3.6).ToString() + " km/h";

                    ret += "\nETA: ";
                    ret += Math.Round((this.Objective.GetValueOrDefault() - this.Position).Length() / this.RelativeVelocity.Z).ToString() + " s";

                    ret += "\n";

                    ret += "\nLatest warning: NOT DEFINED";
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
