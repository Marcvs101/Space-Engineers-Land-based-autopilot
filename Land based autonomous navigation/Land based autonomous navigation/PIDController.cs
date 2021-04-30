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

namespace IngameScript {
    partial class Program {
        public class pidControllerData {
            // Coefficients
            public double kp, ki, kd;
            // Internal data
            public double previousError, integral, derivative, output;

            public pidControllerData(double Kp, double Ki, double Kd) {
                this.kp = Kp;
                this.ki = Ki;
                this.kd = Kd;
                this.previousError = 0;
                this.integral = 0;
                this.derivative = 0;
                this.output = 0;
            }

            public double doPidLoop(double setpoint, double measured_value, double deltaTime) {
                double error = setpoint - measured_value;
                this.integral = this.integral + error * deltaTime;
                this.derivative = (error - this.previousError) / deltaTime;
                this.output = this.kp * error + this.ki * this.integral + this.kd * this.derivative;
                this.previousError = error;
                return this.output;
                // Remember to wait deltaTime;
            }

            public double doProportionalLoop(double setpoint, double measured_value) {
                this.output = this.kp * (setpoint - measured_value);
                return this.output;
            }

            public void disarmPid() {
                this.previousError = 0;
                this.integral = 0;
                this.derivative = 0;
                this.output = 0;
            }

        }
    }
}
