using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace UnHingedIndustries.CSBD {
    public sealed class Program : MyGridProgram {
        const string ScriptVersion = "1.0.1";

        // For immersion purposes only. Set to false if you don't want the booting screen to appear.
        bool _isBooting = true;

        // Value (in milliseconds) the booting screen will appear for.
        const long BootingTimeTotal = 5000;

        HashSet<IMyCubeGrid> _includeGrids;

        readonly List<IMyThrust> _maneuveringThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> _breakingThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> _boostThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> _gravityThrusters = new List<IMyThrust>();

        readonly List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>();
        readonly List<IMyBatteryBlock> _backupBatteries = new List<IMyBatteryBlock>();

        readonly List<IMyLandingGear> _landingGears = new List<IMyLandingGear>();
        readonly List<IMyTimerBlock> _landingGearTimers = new List<IMyTimerBlock>();
        readonly List<IMyFunctionalBlock> _landingGearToggleComponents = new List<IMyFunctionalBlock>();
        readonly List<IMyShipConnector> _parkingConnectors = new List<IMyShipConnector>();

        readonly List<IMyGasTank> _oxygenTanks = new List<IMyGasTank>();
        readonly List<IMyGasTank> _hydrogenTanks = new List<IMyGasTank>();

        readonly List<IMyLightingBlock> _cruiseLights = new List<IMyLightingBlock>();
        readonly List<IMyLightingBlock> _navigationLights = new List<IMyLightingBlock>();
        readonly List<IMyLightingBlock> _searchLights = new List<IMyLightingBlock>();
        readonly List<IMyLightingBlock> _parkingLights = new List<IMyLightingBlock>();

        readonly List<IMyLightingBlock> _cabinLights = new List<IMyLightingBlock>();
        readonly List<IMyTimerBlock> _cabinDoorTimers = new List<IMyTimerBlock>();
        readonly List<IMyFunctionalBlock> _cabinToggleComponents = new List<IMyFunctionalBlock>();
        readonly List<IMyAirVent> _cabinAirVents = new List<IMyAirVent>();
        readonly List<IMyTimerBlock> _hangarDoorTimers = new List<IMyTimerBlock>();
        readonly List<IMyFunctionalBlock> _hangarToggleComponents = new List<IMyFunctionalBlock>();

        static long CurrentTimeMillis() {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        long _bootingStartTime = CurrentTimeMillis();

        void Reboot() {
            _isBooting = true;
            _bootingStartTime = CurrentTimeMillis();
        }

        readonly Color _bootingColor = new Color(200, 200, 50);

        void DisplayBooting() {
            var bootingTimeElapsed = CurrentTimeMillis() - _bootingStartTime;
            var bootingProgressPercent = (double)bootingTimeElapsed / BootingTimeTotal;
            var bootingProgressBarComplete = Convert.ToInt32(15 * bootingProgressPercent);
            if (bootingProgressBarComplete > 15) bootingProgressBarComplete = 15;
            var bootingProgressBar = string.Concat(Enumerable.Repeat("█", bootingProgressBarComplete))
                                     + (bootingProgressBarComplete < 15
                                         ? string.Concat(Enumerable.Repeat(" ", 15 - bootingProgressBarComplete))
                                         : ""
                                     );
            if (GetSettingValue("colors")) Me.GetSurface(0).FontColor = _bootingColor;
            Me.GetSurface(0).WriteText(
                "\n\n\n UNHINGED INDUSTRIES \n"
                + "      CSBD v1.0      \n\n"
                + "   SYSTEM STARTING\n\n"
                + " ┌                 ┐\n"
                + "   " + bootingProgressBar + "\n"
                + " └                 ┘"
            );
            if (bootingTimeElapsed > BootingTimeTotal) _isBooting = false;
        }

        bool GetSettingValue(string settingName) {
            var allSettings = Me.CustomData.Split('\n');
            var settingValue = allSettings.Where(setting => setting.StartsWith(settingName + '='))
                                          .Select(setting => setting.Split('=')[1])
                                          .DefaultIfEmpty("ON")
                                          .First();

            if (settingValue == "OFF") return false;
            return true;
        }

        public class ScreenOption {
            public readonly string Name; // MUST be 11 characters
            public readonly Func<string> GetState; // MUST be 3 characters
            public readonly Action<ScreenOption> OnClick;

            public ScreenOption(string name, Func<string> getState, Action<ScreenOption> onClick) {
                Name = name;
                GetState = getState;
                OnClick = onClick;
            }

            public void Click() {
                OnClick(this);
            }
        }

        public class Screen {
            public readonly string Title; // MUST be 9 characters
            public readonly Color Color;

            // if present, options will be ignored
            public readonly string Content;
            public readonly Action<Screen> OnClick;

            public int SelectedOption;
            public List<ScreenOption> Options; // AT MOST 4

            public Screen(string title, Color color, List<ScreenOption> options) {
                Title = title;
                Color = color;
                Options = options;
            }

            public Screen(string title, Color color, string content, Action<Screen> onClick) {
                Title = title;
                Color = color;
                Content = content;
                OnClick = onClick;
            }
        }

        Screen _welcomeScreen = new Screen(
            "CSBD v1.0",
            new Color(200, 200, 200),
            " Controls:\n"
            + "  7 - this screen\n"
            + "  4 - previous tab\n"
            + "  6 - next tab\n"
            + "  8 - previous option\n"
            + "  2 - next option\n"
            + "  5 - select\n"
            + "  7 - this page\n"
            + "  9 - reset\n",
            screen => { }
        );

        void PopulateBlocksFromGroup<T>(string groupNamePart, List<T> outputBlocksList) where T : class, IMyTerminalBlock {
            outputBlocksList.Clear();
            var blockGroups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(blockGroups, g => g.Name.Contains(groupNamePart));
            var group = blockGroups.Count == 0 ? null : blockGroups[0];
            if (group != null) {
                group.GetBlocksOfType(outputBlocksList, block => _includeGrids.Contains(block.CubeGrid));
            }
        }

        void PopulateBlocks<T>(List<T> outputList, Func<T, bool> filter) where T : class, IMyTerminalBlock {
            outputList.Clear();
            GridTerminalSystem.GetBlocksOfType(outputList, filter);
        }

        void PopulateBlocks<T>(List<T> outputList) where T : class, IMyTerminalBlock {
            outputList.Clear();
            GridTerminalSystem.GetBlocksOfType(outputList, block => true);
        }

        void FindAllMechanicallyConnectedSubgrids(
            List<IMyMechanicalConnectionBlock> mechanicalConnections,
            IMyCubeGrid connectedTo,
            HashSet<IMyCubeGrid> result
        ) {
            result.Add(connectedTo);

            mechanicalConnections.ForEach(mechanicalConnection => {
                if (mechanicalConnection.CubeGrid == connectedTo && !result.Contains(mechanicalConnection.TopGrid)) {
                    FindAllMechanicallyConnectedSubgrids(
                        mechanicalConnections,
                        mechanicalConnection.TopGrid,
                        result
                    );
                }

                if (mechanicalConnection.TopGrid == connectedTo && !result.Contains(mechanicalConnection.CubeGrid)) {
                    FindAllMechanicallyConnectedSubgrids(
                        mechanicalConnections,
                        mechanicalConnection.CubeGrid,
                        result
                    );
                }
            });
        }

        HashSet<IMyCubeGrid> FindAllMechanicallyConnectedSubgrids() {
            var mechanicalConnections = new List<IMyMechanicalConnectionBlock>();
            PopulateBlocks(mechanicalConnections);
            var result = new HashSet<IMyCubeGrid>();
            FindAllMechanicallyConnectedSubgrids(mechanicalConnections, Me.CubeGrid, result);
            return result;
        }

        void PopulateBlocksBySubtype<T>(string subtypePart, List<T> outputList) where T : class, IMyTerminalBlock {
            outputList.Clear();
            PopulateBlocks(outputList, block =>
                _includeGrids.Contains(block.CubeGrid) && block.BlockDefinition.SubtypeId.Contains(subtypePart)
            );
        }

        void PopulateBlocks<T>(string namePart, List<T> outputList) where T : class {
            outputList.Clear();
            var temporaryList = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(namePart, temporaryList, block =>
                _includeGrids.Contains(block.CubeGrid) && block is T
            );
            temporaryList.ForEach(block => outputList.Add(block as T));
        }

        float GetTotalStoredPower(List<IMyBatteryBlock> batteries) {
            return batteries.Select(battery => battery.CurrentStoredPower)
                            .Aggregate(0f, (total, current) => total + current);
        }

        float GetTotalMaxStoredPower(List<IMyBatteryBlock> batteries) {
            return batteries.Select(battery => battery.MaxStoredPower)
                            .Aggregate(0f, (total, current) => total + current);
        }

        string GetPercentage<T>(List<T> blocks, Func<T, float> percentageGetter) where T : IMyTerminalBlock {
            if (blocks.Count == 0) return " -- ";
            if (blocks.Exists(block => !block.IsFunctional)) return " ERR";
            return ((blocks.Aggregate(0f, (acc, current) => acc + percentageGetter(current)) / blocks.Count).ToString("F0") + "%").PadLeft(4);
        }

        string GetStoredPowerPercentage(List<IMyBatteryBlock> batteries) {
            return GetPercentage(batteries, battery => (GetTotalStoredPower(batteries) * 100) / GetTotalMaxStoredPower(batteries));
        }

        string GetStoredGasPercentage(List<IMyGasTank> gasTanks) {
            return GetPercentage(gasTanks, gasTank => (float)(gasTank.FilledRatio * 100));
        }

        string GetEnableState<T>(List<T> blocks) where T : IMyFunctionalBlock {
            if (blocks.Count == 0) return "---";
            if (blocks.Exists(block => !block.IsFunctional)) return "ERR";
            if (blocks.TrueForAll(block => block.Enabled)) return " ON";
            if (blocks.TrueForAll(block => !block.Enabled)) return "OFF";
            return "MIX";
        }

        void ToggleEnableState<T>(ScreenOption option, List<T> blocks) where T : IMyFunctionalBlock {
            var state = option.GetState();
            if (state == " ON" || state == "MIX") {
                blocks.ForEach(block => block.Enabled = false);
            }
            else if (state == "OFF") {
                blocks.ForEach(block => block.Enabled = true);
            }
        }

        string GetBatteriesState(List<IMyBatteryBlock> batteries) {
            if (batteries.Count == 0) return "---";
            if (batteries.Exists(battery => !battery.IsFunctional)) return "ERR";
            if (batteries.TrueForAll(battery => battery.ChargeMode == ChargeMode.Auto)) return "AUT";
            if (batteries.TrueForAll(battery => battery.ChargeMode == ChargeMode.Discharge)) return "DIS";
            if (batteries.TrueForAll(battery => battery.ChargeMode == ChargeMode.Recharge)) return "REC";
            return "MIX";
        }

        void ToggleBatteries(ScreenOption option, List<IMyBatteryBlock> batteries) {
            var state = option.GetState();
            if (state == "AUT") {
                batteries.ForEach(battery => battery.ChargeMode = ChargeMode.Discharge);
            }
            else if (state == "DIS") {
                batteries.ForEach(battery => battery.ChargeMode = ChargeMode.Recharge);
            }
            else if (state == "REC" || state == "MIX") {
                batteries.ForEach(battery => battery.ChargeMode = ChargeMode.Auto);
            }
        }

        string GetAutoLockState(List<IMyLandingGear> landingGears) {
            if (landingGears.Count == 0) return "---";
            if (landingGears.Exists(landingGear => !landingGear.IsFunctional)) return "ERR";
            if (landingGears.TrueForAll(landingGear => landingGear.AutoLock)) return "AUT";
            if (landingGears.TrueForAll(landingGear => !landingGear.AutoLock)) return "MAN";
            return "MIX";
        }

        void ToggleAutoLock(ScreenOption option, List<IMyLandingGear> landingGears) {
            var state = option.GetState();
            if (state == "AUT" || state == "MIX") {
                landingGears.ForEach(landingGear => landingGear.AutoLock = false);
            }
            else if (state == "MAN") {
                landingGears.ForEach(landingGear => landingGear.AutoLock = true);
            }
        }

        string GetLockState<T>(List<T> blocks) where T : IMyFunctionalBlock {
            if (blocks.Count == 0) return "---";
            if (blocks.Exists(landingGear => !landingGear.IsFunctional)) return "ERR";

            Func<T, LandingGearMode> getLockMode;
            var blockType = typeof(T);
            if (blockType == typeof(IMyShipConnector)) {
                getLockMode = connector => {
                    var status = (connector as IMyShipConnector)?.Status;
                    if (status == MyShipConnectorStatus.Unconnected) return LandingGearMode.Unlocked;
                    if (status == MyShipConnectorStatus.Connectable) return LandingGearMode.ReadyToLock;
                    return LandingGearMode.Locked;
                };
            }
            else if (blockType == typeof(IMyLandingGear)) {
                getLockMode = landingGear => (landingGear as IMyLandingGear)?.LockMode ?? LandingGearMode.Unlocked;
            }
            else {
                throw new ArgumentException("cannot get lock state from " + blockType);
            }

            if (blocks.Exists(block => getLockMode(block) == LandingGearMode.Locked)) return "LCK";
            if (blocks.Exists(block => getLockMode(block) == LandingGearMode.ReadyToLock)) return "RDY";
            return "UNL";
        }

        void ToggleLock<T>(ScreenOption option, List<T> blocks) where T : IMyFunctionalBlock {
            Action<T, bool> toggleLock;
            var blockType = typeof(T);
            if (blockType == typeof(IMyShipConnector)) {
                toggleLock = (connector, shouldLock) => {
                    if (shouldLock) (connector as IMyShipConnector)?.Connect();
                    else (connector as IMyShipConnector)?.Disconnect();
                };
            }
            else if (blockType == typeof(IMyLandingGear)) {
                toggleLock = (landingGear, shouldLock) => {
                    if (shouldLock) (landingGear as IMyLandingGear)?.Lock();
                    else (landingGear as IMyLandingGear)?.Unlock();
                };
            }
            else {
                throw new ArgumentException("cannot toggle lock of " + blockType);
            }

            blocks.ForEach(block => toggleLock(block, option.GetState() == "RDY"));
        }

        string GetGasTanksState(List<IMyGasTank> gasTanks) {
            if (gasTanks.Count == 0) return "---";
            if (gasTanks.Exists(tank => !tank.IsFunctional)) return "ERR";
            if (gasTanks.TrueForAll(tank => tank.Stockpile)) return "STK";
            if (gasTanks.TrueForAll(tank => !tank.Stockpile)) return "REL";
            return "MIX";
        }

        void ToggleGasTanks(ScreenOption option, List<IMyGasTank> gasTanks) {
            var state = option.GetState();
            if (state == "STK" || state == "MIX") {
                gasTanks.ForEach(tank => tank.Stockpile = false);
            }
            else if (state == "REL") {
                gasTanks.ForEach(tank => tank.Stockpile = true);
            }
        }

        string GetTimerToggleComponentsState<T>(List<T> components) where T : class, IMyFunctionalBlock {
            if (components.Count == 0) return "---";
            if (components.Exists(timer => !timer.IsFunctional)) return "ERR";

            if (components.Count(component => component.Enabled) > components.Count / 2) {
                return "AAA";
            }

            return "BBB";
        }

        void TriggerTimers(ScreenOption option, List<IMyTimerBlock> timers) {
            var state = option.GetState();
            if (state == "AAA" || state == "BBB") {
                timers.ForEach(timer => timer.ApplyAction("TriggerNow"));
            }
        }

        string GetAirVentsState(List<IMyAirVent> airVents) {
            if (airVents.Count == 0) return "---";
            if (airVents.Exists(vent => !vent.IsFunctional)) return "ERR";
            if (airVents.TrueForAll(vent => vent.Depressurize)) return "DEP";
            if (airVents.TrueForAll(vent => !vent.Depressurize)) return "PRE";
            return "MIX";
        }

        void ToggleAirVents(ScreenOption option, List<IMyAirVent> airVents) {
            var state = option.GetState();
            if (state == "DEP" || state == "MIX") {
                airVents.ForEach(vent => vent.Depressurize = false);
            }
            else if (state == "PRE") {
                airVents.ForEach(vent => vent.Depressurize = true);
            }
        }

        string GetSettingState(string settingName) {
            return GetSettingValue(settingName) ? " ON" : "OFF";
        }

        void ToggleSettingState(string settingName) {
            var wasAlreadySaved = false;
            Me.CustomData = Me.CustomData.Split('\n')
                              .Where(setting => !string.IsNullOrEmpty(setting))
                              .Select<string, string>(setting => {
                                  if (setting.StartsWith(settingName + '=')) {
                                      wasAlreadySaved = true;
                                      if (setting.Split('=')[1] == "ON") return settingName + "=OFF";
                                      return settingName + "=ON";
                                  }

                                  return setting;
                              })
                              .Aggregate("", (acc, line) => acc + line + '\n') +
                            (wasAlreadySaved ? "" : settingName + "=OFF");
        }

        public int CurrentScreenId;
        public List<Screen> AllScreens;
        public List<char> ScreenLetters;

        void Setup() {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            _includeGrids = FindAllMechanicallyConnectedSubgrids();

            PopulateBlocks("Maneuvering", _maneuveringThrusters);
            PopulateBlocks("Breaking", _breakingThrusters);
            PopulateBlocks("Boost", _boostThrusters);
            PopulateBlocks("Gravity", _gravityThrusters);

            PopulateBlocks("Battery", _batteries);
            PopulateBlocks("Backup", _backupBatteries);
            _backupBatteries.ForEach(backupBattery => _batteries.Remove(backupBattery));

            PopulateBlocks("Landing Gear", _landingGears);
            PopulateBlocks("Landing Gear", _landingGearTimers);
            PopulateBlocksFromGroup("Landing Gear Toggle Components", _landingGearToggleComponents);
            PopulateBlocks("Parking", _parkingConnectors);

            PopulateBlocksBySubtype("Oxygen", _oxygenTanks);
            PopulateBlocksBySubtype("Hydrogen", _hydrogenTanks);

            PopulateBlocks("Cruise", _cruiseLights);
            PopulateBlocks("Navigation", _navigationLights);
            PopulateBlocks("Search", _searchLights);
            PopulateBlocks("Parking", _parkingLights);

            PopulateBlocks("Cabin", _cabinLights);
            PopulateBlocks("Cabin", _cabinDoorTimers);
            PopulateBlocksFromGroup("Cabin Toggle Components", _cabinToggleComponents);
            PopulateBlocks("Cabin", _cabinAirVents);
            PopulateBlocks("Hangar", _hangarDoorTimers);
            PopulateBlocksFromGroup("Hangar Toggle Components", _hangarToggleComponents);

            AllScreens = new List<Screen> {
                    new Screen(
                        "THRUSTERS",
                        new Color(200, 80, 80),
                        new List<ScreenOption> {
                            new ScreenOption(
                                "BOOST      ", () => GetEnableState(_boostThrusters),
                                option => ToggleEnableState(option, _boostThrusters)
                            ),
                            new ScreenOption(
                                "BREAKING   ", () => GetEnableState(_breakingThrusters),
                                option => ToggleEnableState(option, _breakingThrusters)
                            ),
                            new ScreenOption(
                                "MANEUVERING", () => GetEnableState(_maneuveringThrusters),
                                option => ToggleEnableState(option, _maneuveringThrusters)
                            ),
                            new ScreenOption(
                                "GRAVITY    ", () => GetEnableState(_gravityThrusters),
                                option => ToggleEnableState(option, _gravityThrusters)
                            )
                        }
                    ),
                    new Screen(
                        "BATTERIES",
                        new Color(50, 50, 200),
                        new List<ScreenOption> {
                            new ScreenOption(
                                "MAIN       ", () => GetBatteriesState(_batteries),
                                option => ToggleBatteries(option, _batteries)
                            ),
                            new ScreenOption(
                                "BACKUP     ", () => GetBatteriesState(_backupBatteries),
                                option => ToggleBatteries(option, _backupBatteries)
                            )
                        }
                    ),
                    new Screen(
                        " PARKING ",
                        new Color(150, 150, 150),
                        new List<ScreenOption> {
                            new ScreenOption(
                                "GEAR LOCK  ", () => GetLockState(_landingGears),
                                option => ToggleLock(option, _landingGears)
                            ),
                            new ScreenOption(
                                "GEAR MODE  ", () => GetAutoLockState(_landingGears),
                                option => ToggleAutoLock(option, _landingGears)
                            ),
                            new ScreenOption(
                                "GEAR EXTEND", () => GetTimerToggleComponentsState(_landingGearToggleComponents),
                                option => TriggerTimers(option, _landingGearTimers)
                            ),
                            new ScreenOption(
                                "CONN LOCK  ", () => GetLockState(_parkingConnectors),
                                option => ToggleLock(option, _parkingConnectors)
                            )
                        }
                    ),
                    new Screen(
                        "GAS TANKS",
                        new Color(50, 200, 50),
                        new List<ScreenOption> {
                            new ScreenOption(
                                "OXYGEN     ", () => GetGasTanksState(_oxygenTanks),
                                option => ToggleGasTanks(option, _oxygenTanks)
                            ),
                            new ScreenOption(
                                "HYDROGEN   ", () => GetGasTanksState(_hydrogenTanks),
                                option => ToggleGasTanks(option, _hydrogenTanks)
                            )
                        }
                    ),
                    new Screen(
                        "  LIGHT  ",
                        new Color(50, 200, 200),
                        new List<ScreenOption> {
                            new ScreenOption(
                                "CRUISE     ", () => GetEnableState(_cruiseLights),
                                option => ToggleEnableState(option, _cruiseLights)
                            ),
                            new ScreenOption(
                                "NAVIGATION ", () => GetEnableState(_navigationLights),
                                option => ToggleEnableState(option, _navigationLights)
                            ),
                            new ScreenOption(
                                "SEARCH     ", () => GetEnableState(_searchLights),
                                option => ToggleEnableState(option, _searchLights)
                            ),
                            new ScreenOption(
                                "PARKING    ", () => GetEnableState(_parkingLights),
                                option => ToggleEnableState(option, _parkingLights)
                            )
                        }
                    ),
                    new Screen(
                        "  CABIN  ",
                        new Color(200, 50, 200),
                        new List<ScreenOption> {
                            new ScreenOption(
                                "LIGHTS     ", () => GetEnableState(_cabinLights),
                                option => ToggleEnableState(option, _cabinLights)
                            ),
                            new ScreenOption(
                                "DOOR       ", () => GetTimerToggleComponentsState(_cabinToggleComponents),
                                option => TriggerTimers(option, _cabinDoorTimers)
                            ),
                            new ScreenOption(
                                "AIR VENT   ", () => GetAirVentsState(_cabinAirVents),
                                option => ToggleAirVents(option, _cabinAirVents)
                            ),
                            new ScreenOption(
                                "HANGAR     ", () => GetTimerToggleComponentsState(_hangarToggleComponents),
                                option => TriggerTimers(option, _hangarDoorTimers)
                            )
                        }
                    ),
                    new Screen(
                        "  SETUP  ",
                        new Color(200, 200, 200),
                        new List<ScreenOption> {
                            new ScreenOption(
                                "COLORS     ", () => GetSettingState("colors"),
                                option => ToggleSettingState("colors")
                            ),
                            new ScreenOption(
                                "SCROLL BAR ", () => GetSettingState("scrollBar"),
                                option => ToggleSettingState("scrollBar")
                            ),
                            new ScreenOption(
                                "TOP STATUS ", () => GetSettingState("showTopStatus"),
                                option => ToggleSettingState("showTopStatus")
                            ),
                            new ScreenOption(
                                "SHOW EMPTY ", () => GetSettingState("showEmpty"),
                                option => {
                                    ToggleSettingState("showEmpty");
                                    Save();
                                    Setup();
                                    CurrentScreenId = AllScreens.Count - 1;
                                }
                            )
                        }
                    )
                }.Select(screen => {
                     if (screen.Options != null) {
                         screen.Options = GetSettingValue("showEmpty")
                             ? screen.Options
                             : screen.Options.Where(option =>
                                 option.GetState() != "---"
                             ).ToList();
                     }

                     return screen;
                 }).Where(screen => screen.Options == null || screen.Options.Count > 0)
                 .ToList();

            if (string.IsNullOrEmpty(Storage) && !int.TryParse(Storage, out CurrentScreenId)) {
                CurrentScreenId = -1;
            }

            if (CurrentScreenId >= AllScreens.Count) CurrentScreenId = -1;

            ScreenLetters = AllScreens.Select(screen => screen.Title.Trim()[0]).ToList();

            Me.GetSurface(0).ContentType = ContentType.TEXT_AND_IMAGE;
            Me.GetSurface(0).Font = "Monospace";
            Me.GetSurface(0).FontSize = 1.24F;
        }

        public Program() {
            Setup();
        }

        public void Save() {
            Storage = CurrentScreenId.ToString();
        }

        public void Main(string argument, UpdateType updateSource) {
            if (_isBooting) {
                DisplayBooting();
                return;
            }

            Screen currentScreen;
            List<ScreenOption> options;

            if (updateSource == UpdateType.Trigger) {
                currentScreen = CurrentScreenId == -1 ? _welcomeScreen : AllScreens[CurrentScreenId];
                options = currentScreen.Options;
                if (CurrentScreenId == -1) {
                    CurrentScreenId = 0;
                }
                else if (argument.Equals("CLICK")) {
                    options[currentScreen.SelectedOption].Click();
                }
                else if (argument.Equals("UP")) {
                    currentScreen.SelectedOption -= 1;
                    if (currentScreen.SelectedOption < 0) currentScreen.SelectedOption = options.Count - 1;
                }
                else if (argument.Equals("DOWN")) {
                    currentScreen.SelectedOption += 1;
                    if (currentScreen.SelectedOption >= options.Count) currentScreen.SelectedOption = 0;
                }
                else if (argument.Equals("LEFT")) {
                    CurrentScreenId -= 1;
                    if (CurrentScreenId < 0) CurrentScreenId = AllScreens.Count - 1;
                }
                else if (argument.Equals("RIGHT")) {
                    CurrentScreenId += 1;
                    if (CurrentScreenId >= AllScreens.Count) CurrentScreenId = 0;
                }
                else if (argument.Equals("WELCOME")) {
                    CurrentScreenId = -1;
                }
                else if (argument.Equals("RESET")) {
                    Save();
                    Setup();
                    Reboot();
                }
            }

            // need to do the filtering again in case if screen changed by action
            currentScreen = CurrentScreenId == -1 ? _welcomeScreen : AllScreens[CurrentScreenId];
            options = currentScreen.Options;

            var optionsStringBuilder = new StringBuilder();
            if (currentScreen.Content != null) {
                optionsStringBuilder.Append(currentScreen.Content);
            }
            else {
                for (var i = 0; i < options.Count; ++i) {
                    var currentOption = options[i];
                    if (currentScreen.SelectedOption == i) {
                        optionsStringBuilder.Append(" ┌                 ┐\n");
                    }
                    else if (currentScreen.SelectedOption == i - 1) {
                        optionsStringBuilder.Append(" └                 ┘\n");
                    }
                    else {
                        optionsStringBuilder.Append('\n');
                    }

                    optionsStringBuilder.Append("   " + currentOption.Name + ' ' + currentOption.GetState() + '\n');
                    if (currentScreen.SelectedOption == i) {
                        optionsStringBuilder.Append(" └                 ┘");
                    }
                }
            }

            var screenLettersToPrint = "";
            if (GetSettingValue("scrollBar")) {
                screenLettersToPrint = ScreenLetters.Select((letter, index) => index == CurrentScreenId ? "[ " + letter + " ]" : letter.ToString())
                                                    .Aggregate("", (result, currentLetter) => result + currentLetter);
                if (CurrentScreenId == -1) screenLettersToPrint = "[ " + screenLettersToPrint + " ]";
                var padLetters = ((21 - screenLettersToPrint.Length) / 2) + screenLettersToPrint.Length;
                screenLettersToPrint = screenLettersToPrint.PadLeft(padLetters);
            }

            var topStatus = GetSettingValue("showTopStatus")
                ? "[BAT " + GetStoredPowerPercentage(_batteries) + "] "
                  + "[H2  " + GetStoredGasPercentage(_hydrogenTanks) + "]\n"
                  + "[BCK " + GetStoredPowerPercentage(_backupBatteries) + "] "
                  + "[O2  " + GetStoredGasPercentage(_oxygenTanks) + "]\n"
                : "";

            if (GetSettingValue("colors")) Me.GetSurface(0).FontColor = currentScreen.Color;
            Me.GetSurface(0).WriteText(
                topStatus
                + screenLettersToPrint + "\n\n"
                + "  ─── " + currentScreen.Title + " ───\n"
                + optionsStringBuilder
            );
        }
    }
}