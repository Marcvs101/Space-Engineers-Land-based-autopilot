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
        public class WheelController
        {
            public double Heading { get; set; }
            public float Power { get; set; }
            public float Steering { get; set; }

            private Dictionary<bool, List<IMyMotorSuspension>> propulsionDirection = new Dictionary<bool, List<IMyMotorSuspension>>();
            private Dictionary<bool, List<IMyMotorSuspension>> steeringDirection = new Dictionary<bool, List<IMyMotorSuspension>>();
            private List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();

            public WheelController(IMyGridTerminalSystem myGridTerminalSystem, IMyShipController controlReference)
            {
                steeringDirection.Add(true, new List<IMyMotorSuspension>());
                steeringDirection.Add(false, new List<IMyMotorSuspension>());

                propulsionDirection.Add(true, new List<IMyMotorSuspension>());
                propulsionDirection.Add(false, new List<IMyMotorSuspension>());

                myGridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(wheels);

                Vector3D propulsionCenter = new Vector3D();
                foreach (IMyMotorSuspension w in wheels)
                {
                    if (w != null)
                    {
                        propulsionCenter += w.GetPosition();
                    }
                }
                propulsionCenter /= wheels.Count;

                foreach (IMyMotorSuspension w in wheels)
                {
                    if (w != null)
                    {
                        Vector3D relPos = Vector3D.TransformNormal(w.GetPosition() - propulsionCenter, MatrixD.Transpose(controlReference.WorldMatrix));
                        if (relPos.Z <= 0)
                        {
                            steeringDirection[true].Add(w);
                        }
                        else
                        {
                            steeringDirection[false].Add(w);
                        }
                        if(relPos.X <= 0)
                        {
                            propulsionDirection[true].Add(w);
                        }
                        else
                        {
                            propulsionDirection[false].Add(w);
                        }
                    }
                }
                ReleaseWheels();
            }

            public void SteeringDirection(Vector3D relativeTarget, Vector3D relativeVelocity, float speedLimit)
            {// TODO do not calculate steering on every cycle
                this.Heading = (double)(-Math.Atan2(relativeTarget.X, relativeTarget.Z));
                double target = this.Heading;

                // Normalize in -1:1 range
                if (target > 1) target = 1;
                else if (target < -1) target = -1;

                float actualTarget = (float)target;

                // No hardsteering at speed
                if (Math.Abs(target) > .1) actualTarget = (float)(target / (((Math.Abs(relativeVelocity.Z) / (double)speedLimit) * 5) * .9 + .1));

                // Move wheels
                foreach (IMyMotorSuspension w in steeringDirection[true]) { w.SetValue("Steer override", actualTarget); }
                foreach (IMyMotorSuspension w in steeringDirection[false]) { w.SetValue("Steer override", -actualTarget); }

                this.Steering = (float)target; // return steering (to be set in autopilot)
            }

            public void ReleaseWheels()
            {
                foreach (IMyMotorSuspension w in wheels)
                {
                    w.SetValue("Propulsion override", 0.0f);
                    w.SetValue("Steer override", 0.0f);
                }
            }

            public void moveWheels(Vector3D RelativeVelocity, float speed, float powerFactor)
            {
                this.Power = (speed - ((float)RelativeVelocity.Z)) / powerFactor;

                // Normalize in in -1:1 range
                if (this.Power > 1) this.Power = 1f;
                else if (this.Power < -1) this.Power = -1f;

                foreach (IMyMotorSuspension w in propulsionDirection[true]) { w.SetValue("Propulsion override", this.Power); }
                foreach (IMyMotorSuspension w in propulsionDirection[false]) { w.SetValue("Propulsion override", -this.Power); }
            }
        }
    }
}
