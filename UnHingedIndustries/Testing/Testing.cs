using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace UnHingedIndustries.Testing {
    public sealed class Program : MyGridProgram {
        const string ScriptVersion = "0.0.1";
        const string WorkshopItemId = "3042812703";
        const string ModIoItemId = "0";

        public Program() {
            // Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        public void Main(string argument, UpdateType updateSource) {
            var shipController = FindShipController(Me, GridTerminalSystem);
            var gyroscopes = FindGyroscopes(Me, GridTerminalSystem);

            var shipMass = shipController.CalculateShipMass();

            var positionInWorldCoordinates = shipController.GetPosition();
            var gravityVector = shipController.GetTotalGravity();
            var velocityVector = shipController.CubeGrid.LinearVelocity;

            var controllerOrientation = Quaternion.Zero;
            shipController.Orientation.GetQuaternion(out controllerOrientation);

            var planetCenterPosition = Vector3D.Zero;
            shipController.TryGetPlanetPosition(out planetCenterPosition);

            var surfaceElevation = 0.0d;
            shipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out surfaceElevation);

            var thrusters = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(thrusters);
            var thrusterOrientations = string.Join(
                '\n',
                thrusters.Select(thruster => {
                    var thrusterOrientationQuaternion = Quaternion.Zero;
                    thruster.Orientation.GetQuaternion(out thrusterOrientationQuaternion);
                    return "  " + thruster.Name + ") " + thruster.CustomName + ": " + thrusterOrientationQuaternion;
                })
            );

            Me.GetSurface(0).WriteText(
                "Base mass: " + shipMass.BaseMass + '\n' +
                "Physical mass: " + shipMass.PhysicalMass + '\n' +
                "Total mass: " + shipMass.TotalMass + '\n' +
                "Position in world coordinates: " + positionInWorldCoordinates + '\n' +
                "Controller orientation: " + controllerOrientation + '\n' +
                "Planet center position: " + planetCenterPosition + '\n' +
                "Gravity vector: " + gravityVector + '\n' +
                "Surface elevation: " + surfaceElevation + '\n' +
                "Velocity vector: " + velocityVector + '\n' +
                "Cockpit to world transform: "+ GetBlock2WorldTransform(shipController) +
                "Ship to world transform: "+ GetGrid2WorldTransform(shipController.CubeGrid) +
                "Thruster orientation:" + thrusterOrientations
            );
        }
        
        MatrixD GetBlock2WorldTransform(IMyCubeBlock blk) {
            Matrix blk2grid;
            blk.Orientation.GetMatrix(out blk2grid);
            return blk2grid*
                   MatrixD.CreateTranslation(new Vector3D(blk.Min+blk.Max)/2.0)*
                   GetGrid2WorldTransform(blk.CubeGrid);
        }
        
        MatrixD GetGrid2WorldTransform(IMyCubeGrid grid)
        {
            Vector3D origin=grid.GridIntegerToWorld(new Vector3I(0,0,0));
            Vector3D plusY=grid.GridIntegerToWorld(new Vector3I(0,1,0))-origin;
            Vector3D plusZ=grid.GridIntegerToWorld(new Vector3I(0,0,1))-origin;
            return MatrixD.CreateScale(grid.GridSize)*MatrixD.CreateWorld(origin,-plusZ,plusY);
        }


        static List<IMyGyro> FindGyroscopes(IMyProgrammableBlock thisProgrammableBlock, IMyGridTerminalSystem gridTerminalSystem) {
            var gyroscopes = new List<IMyGyro>();
            gridTerminalSystem.GetBlocksOfType(gyroscopes);
            return gyroscopes.Where(it => it.CubeGrid == thisProgrammableBlock.CubeGrid)
                             .ToList();
        }

        static IMyShipController FindShipController(IMyProgrammableBlock thisProgrammableBlock, IMyGridTerminalSystem gridTerminalSystem) {
            var controllers = new List<IMyShipController>();
            gridTerminalSystem.GetBlocksOfType(controllers);
            return controllers.Where(it => it.CubeGrid == thisProgrammableBlock.CubeGrid)
                              .Where(it => it.IsMainCockpit)
                              .DefaultIfEmpty(controllers.FirstOrDefault())
                              .First();
        }
    }
}