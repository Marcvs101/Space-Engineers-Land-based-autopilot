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
        public class CollisionController
        {
            public List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
            public bool enabled;
            private MyDetectedEntityInfo info;

            public CollisionController(IMyGridTerminalSystem myGridTerminalSystem, bool enabled)
            {
                this.enabled = enabled;
                List<IMyCameraBlock> blocks = new List<IMyCameraBlock>();
                myGridTerminalSystem.GetBlocksOfType(blocks);
                foreach(IMyCameraBlock cam in blocks)
                {
                    if (cam.CustomName.Contains("[RC]"))
                    {// TODO check camera direction and aligment!
                        cameras.Add(cam);
                        cam.EnableRaycast = true;
                    }
                }
            }

            public double checkForCollisions(double scanDistance)
            {
                double destinationTillCollision = -1f;
                foreach(IMyCameraBlock cam in cameras)
                {
                    if (!cam.CanScan(scanDistance))
                        continue;
                    info = cam.Raycast(scanDistance, 0, 0);
                    if(!info.IsEmpty())
                    {
                        destinationTillCollision = Vector3D.Distance(cam.GetPosition(), info.HitPosition.Value);
                        break;
                    }
                }
                return destinationTillCollision;
            }
        }
    }
}
