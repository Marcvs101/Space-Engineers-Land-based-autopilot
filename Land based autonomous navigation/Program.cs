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

        //##################################
        //BEHAVIOUR VARIABLES
        //##################################

        float POWER_FACTOR = 20f;
        int PRECISION_FACTOR = 10;

        //##################################
        //NO MODIFICATIONS BEYOND THIS POINT
        //##################################

        private List<IMyMotorSuspension> ruote;
        private Dictionary<bool, List<IMyMotorSuspension>> direzionePropulsione;
        private Dictionary<bool, List<IMyMotorSuspension>> direzioneSterzo;

        private List<IMyRemoteControl> controllori;

        private IMyRemoteControl pilotaRemoto;

        private IMyShipController riferimento;

        private List<IMyTextPanel> schermi;

        private class Autopilota {
            public Vector3D? Obiettivo { get; set; }
            public double Rotta { get; set; }

            public Autopilota() {}
        }

        private Autopilota autopilota;

        //private List<string> listaAllerta;

        private MyCommandLine rigaDiComando = new MyCommandLine();
        private Dictionary<string, Action> comandi = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        private class StatoVeicolo {
            public Vector3D Posizione { get; set; }
            public Vector3D VelocitaRelativa { get; set; }
            public MyShipVelocities VelocitaAssoluta { get; set; }
            public Vector3D Orientamento { get; set; }

            public StatoVeicolo() {}

        }

        private StatoVeicolo stato;

        public Program() {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set RuntimeInfo.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            //listaAllerta = new List<string>();

            //Comandi
            comandi["AddWaypoint"] = delegate {
                if (rigaDiComando.Argument(1) != null) {
                    double cmp1, cmp2, cmp3;
                    bool successo1 = double.TryParse(rigaDiComando.Argument(1), out cmp1);
                    bool successo2 = double.TryParse(rigaDiComando.Argument(2), out cmp2);
                    bool successo3 = double.TryParse(rigaDiComando.Argument(3), out cmp3);
                    if (successo1 && successo2 && successo3) {
                        pilotaRemoto.AddWaypoint(new MyWaypointInfo("AutoWaypoint" ,new Vector3D(cmp1, cmp2, cmp3)));
                        Echo("New destination set");
                    } else {
                        //listaAllerta.Add("[CMD] Error on destination format");
                    }
                }
            };
            comandi["SpeedLimit"] = delegate {
                if (rigaDiComando.Argument(1) != null) {
                    int obiettivo;
                    bool successo = int.TryParse(rigaDiComando.Argument(1), out obiettivo);
                    if (successo) {
                        pilotaRemoto.SpeedLimit = (float)obiettivo/3.6f;
                        Echo("New speed limit set");
                    } else {
                        //listaAllerta.Add("[CMD] Error on speed limit format");
                    }
                }
            };
            comandi["Engage"] = delegate { pilotaRemoto.SetAutoPilotEnabled(true); };
            comandi["Disengage"] = delegate { pilotaRemoto.SetAutoPilotEnabled(false); };

            //Controllori
            controllori = new List<IMyRemoteControl>();
            GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(controllori);

            foreach (IMyRemoteControl c in controllori) {
                if (c != null) {
                    pilotaRemoto = c;
                    riferimento = pilotaRemoto;
                }
            }

            //Schermi
            schermi = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(schermi);

            foreach (IMyTextPanel t in schermi) {
                if (t != null) {
                    if (t.CustomName.ToLower().Contains("autopilot")) {
                        t.WritePublicTitle("Autopilot Status");
                        t.WritePublicText("Autopilot compiled correctly\nWaiting for system start");
                    } else if (t.CustomName.ToLower().Contains("hud")) {
                        t.WritePublicTitle("HUD");
                        t.WritePublicText("Autopilot compiled correctly\nWaiting for system start");
                    }
                }
            }

            //Ruote
            ruote = new List<IMyMotorSuspension>();
            GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(ruote);

            direzioneSterzo = new Dictionary<bool, List<IMyMotorSuspension>>();
            direzioneSterzo.Add(true, new List<IMyMotorSuspension>());
            direzioneSterzo.Add(false, new List<IMyMotorSuspension>());

            Vector3D centroPropulsione = new Vector3D();
            foreach (IMyMotorSuspension w in ruote) {
                if (w != null) {
                    centroPropulsione += w.GetPosition();
                }
            }
            centroPropulsione /= ruote.Count;

            foreach (IMyMotorSuspension w in ruote) {
                if (w != null) {
                    Vector3D relPos = Vector3D.TransformNormal(w.GetPosition() - centroPropulsione, MatrixD.Transpose(riferimento.WorldMatrix));
                    if (relPos.Z <= 0) {
                        direzioneSterzo[true].Add(w);
                    } else {
                        direzioneSterzo[false].Add(w);
                    }
                }
            }

            direzionePropulsione = new Dictionary<bool, List<IMyMotorSuspension>>();
            direzionePropulsione.Add(true, new List<IMyMotorSuspension>());
            direzionePropulsione.Add(false, new List<IMyMotorSuspension>());

            foreach (IMyMotorSuspension w in ruote) {
                if (w != null) {
                    Vector3D relPos = Vector3D.TransformNormal(w.GetPosition() - centroPropulsione, MatrixD.Transpose(riferimento.WorldMatrix));
                    if (relPos.X <= 0) {
                        direzionePropulsione[true].Add(w);
                    } else {
                        direzionePropulsione[false].Add(w);
                    }
                }
            }

            //Strutture
            autopilota = new Autopilota();
            pilotaRemoto.SpeedLimit = 22f;//80km/h

            stato = new StatoVeicolo();
            stato.Posizione = riferimento.GetPosition();
            stato.VelocitaAssoluta = riferimento.GetShipVelocities();
            stato.VelocitaRelativa = -Vector3D.TransformNormal((riferimento.GetShipVelocities().LinearVelocity), MatrixD.Transpose(riferimento.WorldMatrix));

            RilasciaRuote();

            //listaAllerta.Add("[SYS] Autopilot booted up correctly");
            Echo("Autopilot booted up correctly");
        }

        public void Save() {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.

        }

        //Schermi
        public string DisplayHUD() {
            string ret = "";
            int velZ = (int)Math.Round(stato.VelocitaRelativa.Z * 3.6);
            int velY = (int)Math.Round(stato.VelocitaRelativa.Y * 3.6);
            int velX = (int)Math.Round(stato.VelocitaRelativa.X * 3.6);

            ret += "Speed: " + velZ.ToString() + " km/h";

            if (velY > 3 || velY < -3) {
                ret += " | Vert!";
            }

            if (velX > 2 || velX < -2) {
                ret += " | Lat!";
            }

            ret += "\nAutopilot: ";
            if (pilotaRemoto.IsAutoPilotEnabled) {
                ret += "Enabled";
            } else {
                ret += "Disabled";
            }

            return ret;
        }

        public string DisplayAutopilota() {
            string ret = "Autopilot Status\n";

            if (pilotaRemoto.IsAutoPilotEnabled) {
                ret += "\nAutopilot enabled\n";

                if (Math.Abs(stato.VelocitaRelativa.Z - pilotaRemoto.SpeedLimit) > 2) {
                    ret += "\nAdjusting Speed";
                } else if (Math.Abs(autopilota.Rotta) > 0.2) {
                    ret += "\nAdjusting Course";
                } else {
                    ret += "\nOn Cruise";
                }

                ret += "\n";

                ret += "\nDistance to waypoint: ";
                ret += Math.Round((autopilota.Obiettivo.GetValueOrDefault() - stato.Posizione).Length()).ToString() + " m";

                ret += "\nCurrent speed: ";
                ret += Math.Round(stato.VelocitaRelativa.Z * 3.6).ToString() + " km/h";

                ret += "\nETA: ";
                ret += Math.Round((autopilota.Obiettivo.GetValueOrDefault() - stato.Posizione).Length() / stato.VelocitaRelativa.Z).ToString() + " s";

                ret += "\n";

                ret += "\nLatest warning: NOT DEFINED";
            } else {
                ret += "\nAutopilot disabled";
            }

            return ret;
        }

        //Navigazione
        public void Crociera(float velocita) {
            //Calcola potenza
            float potenza = (velocita - ((float)stato.VelocitaRelativa.Z)) / POWER_FACTOR;

            //Normalizza in -1:1 range
            if (potenza > 1) potenza = 1;
            else if (potenza < -1) potenza = -1;

            //Muovi ruote
            foreach (IMyMotorSuspension w in direzionePropulsione[true]) {
                w.SetValue("Propulsion override", potenza);
            }

            foreach (IMyMotorSuspension w in direzionePropulsione[false]) {
                w.SetValue("Propulsion override", -potenza);
            }
        }

        public void Direzione(double obiettivo) {
            //Calcola Sterzo
            float sterzo = (float)(obiettivo);

            //Normalizza in -1:1 range
            if (sterzo > 1) sterzo = 1;
            else if (sterzo < -1) sterzo = -1;

            //Muovi ruote
            foreach (IMyMotorSuspension w in direzioneSterzo[true]) {
                w.SetValue("Steer override", sterzo);
            }

            foreach (IMyMotorSuspension w in direzioneSterzo[false]) {
                w.SetValue("Steer override", -sterzo);
            }
        }

        public void RilasciaRuote() {
            foreach (IMyMotorSuspension w in ruote) {
                w.SetValue("Propulsion override", 0.0f);
                w.SetValue("Steer override", 0.0f);
            }
        }

        //Funzione principale autopilota
        public void DoAutopilota() {
            if (autopilota.Obiettivo != null) {

                Crociera(pilotaRemoto.SpeedLimit);
                
                Vector3D obiettivoRelativo = Vector3D.TransformNormal(stato.Posizione - autopilota.Obiettivo.GetValueOrDefault(), MatrixD.Transpose(riferimento.WorldMatrix));

                //Calcola Sterzo
                autopilota.Rotta = (double)(-Math.Atan2(obiettivoRelativo.X, obiettivoRelativo.Z));

                Direzione(autopilota.Rotta);

                Echo("DIST: " + Math.Sqrt(Math.Pow(obiettivoRelativo.X, 2) + Math.Pow(obiettivoRelativo.Z, 2)) + " - " + PRECISION_FACTOR);
                if (Math.Sqrt(Math.Pow(obiettivoRelativo.X,2) + Math.Pow(obiettivoRelativo.Z, 2)) < PRECISION_FACTOR) {
                    autopilota.Obiettivo = null;
                    //listaAllerta.Add("[SYS] AP destination reached");
                }
            } else {
                //listaAllerta.Add("[SYS] AP error: no destination set");
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

            //Parse comandi
            if (rigaDiComando.TryParse(argument)) {
                Action azioneComando;

                var comando = rigaDiComando.Argument(0);
                if (comando == null) {
                    Echo("No command specified");
                } else if (comandi.TryGetValue(comando, out azioneComando)) {
                    azioneComando();
                } else {
                    Echo($"Unknown command {comando}");
                }
            }

            //Update stato
            stato.Posizione = riferimento.GetPosition();
            stato.VelocitaAssoluta = riferimento.GetShipVelocities();
            stato.VelocitaRelativa = -Vector3D.TransformNormal((riferimento.GetShipVelocities().LinearVelocity), MatrixD.Transpose(riferimento.WorldMatrix));

            //Schermi
            foreach (IMyTextPanel t in schermi) {
                if (t != null) {
                    if (t.CustomName.ToLower().Contains("autopilot")) {
                        t.WritePublicText(DisplayAutopilota());
                    } else if (t.CustomName.ToLower().Contains("hud")) {
                        t.WritePublicText(DisplayHUD());
                    }
                }
            }

            //Autopilota
            if (pilotaRemoto.IsAutoPilotEnabled) {
                DoAutopilota();

                if (autopilota.Obiettivo == null) {
                    List<MyWaypointInfo> rotta = new List<MyWaypointInfo>();
                    pilotaRemoto.GetWaypointInfo(rotta);

                    if (rotta.Count > 0) {
                        MyWaypointInfo prossimo = rotta.First();
                        rotta.RemoveAt(0);

                        if (pilotaRemoto.FlightMode == FlightMode.OneWay) {
                            pilotaRemoto.ClearWaypoints();
                            foreach (MyWaypointInfo wp in rotta) {
                                pilotaRemoto.AddWaypoint(wp);
                            }
                        } else if (pilotaRemoto.FlightMode == FlightMode.Circle) {
                            rotta.Add(prossimo);
                            pilotaRemoto.ClearWaypoints();
                            foreach (MyWaypointInfo wp in rotta) {
                                pilotaRemoto.AddWaypoint(wp);
                            }
                        } else { //Patrol
                            //Not implemented
                            pilotaRemoto.SetAutoPilotEnabled(false);
                            Echo("[AP] Patrol not implemented yet!");
                            return;
                        }

                        autopilota.Obiettivo = prossimo.Coords;
                        pilotaRemoto.SetAutoPilotEnabled(true);
                    } else {
                        //No waypoints in list
                        pilotaRemoto.SetAutoPilotEnabled(false);
                    }
                    //Speed up clock
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                }
            
            } else if (Runtime.UpdateFrequency != UpdateFrequency.Update100) {
                //Autopilot turning off
                //Slow down clock for better CPU usage
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                autopilota.Obiettivo = null;
                RilasciaRuote();
                Echo("[AP] Autopilot Disengaged");
            }
        }
    }
}