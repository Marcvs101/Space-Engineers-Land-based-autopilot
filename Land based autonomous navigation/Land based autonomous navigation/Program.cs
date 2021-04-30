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
        int SCAN_DISTANCE = 50; // collision scan distance

        // ##################################
        // NO MODIFICATIONS BEYOND THIS POINT
        // ##################################

        private IMyShipController controlReference;

        // Screens
        private List<IMyTextPanel> screens;

        private UIManager uIManager;
        private AutoPilotManager autopilotManager;

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

            autopilotManager = new AutoPilotManager(GridTerminalSystem, uIManager, POWER_FACTOR, PRECISION_FACTOR, SCAN_DISTANCE);

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
                    if (autopilotManager.onUpdate())
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                        uIManager.printOnScreens("autopilot", autopilotManager.DisplayAutopilotInfo());
                        uIManager.printOnScreens("hud", autopilotManager.DisplayHUD());
                    }
                    else
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update100;
                        uIManager.printOnScreens("service", "[AP] Autopilot Disengaged");
                    }
                    
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
        }
    }
}