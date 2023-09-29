using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace UnHingedIndustries.Testing {
    public sealed class Program : MyGridProgram {
        const string ScriptVersion = "0.0.1";
        const string WorkshopItemId = "3042812703";
        const string ModIoItemId = "0";

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        int _peakRuntimeComplexity = 0;
        public void Main(string argument, UpdateType updateSource) {
            var currentComplexity = GetScriptComplexity();
            if (currentComplexity > _peakRuntimeComplexity) {
                _peakRuntimeComplexity = currentComplexity;
            }

            Me.GetSurface(0).WriteText("Complexity: " + currentComplexity.ToString());
        }

        int GetScriptComplexity() {
            return (Runtime.CurrentInstructionCount * 100) / Runtime.MaxInstructionCount;
        }
    }
}