using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace UnHingedIndustries.uhiDISCO {
    public sealed class Program : MyGridProgram {
        const string ScriptVersion = "0.0.1";
        const string WorkshopItemId = "???";
        const string ModIoItemId = "???";

        public static T GetEnumFromString<T>(string value) where T : struct {
            T result;
            if (Enum.TryParse(value, true, out result)) {
                return result;
            }

            throw new ArgumentException("Invalid value '" + value + "' for enum " + nameof(T));
        }

        static long CurrentTimeMillis() {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        List<IMyLightingBlock>[,] BuildDisplay(IMyGridTerminalSystem gridTerminalSystem) {
            var rgbLightNameRegex = new System.Text.RegularExpressions.Regex(
                @"\bRGB\:([0-9]+):([0-9]+)\b",
                System.Text.RegularExpressions.RegexOptions.Compiled
            );

            var searchResults = new List<IMyLightingBlock>();
            var maxRow = 0;
            var maxColumn = 0;
            gridTerminalSystem.GetBlocksOfType(searchResults, searchResult => {
                var match = rgbLightNameRegex.Match(searchResult.CustomName);
                if (!match.Success) return false;
                var row = Int32.Parse(match.Groups[1].Captures[0].Value);
                if (row > maxRow) maxRow = row;
                var column = Int32.Parse(match.Groups[2].Captures[0].Value);
                if (column > maxColumn) maxColumn = column;
                return true;
            });

            var display = new List<IMyLightingBlock>[maxRow + 1, maxColumn + 1];
            searchResults.ForEach(searchResult => {
                var match = rgbLightNameRegex.Match(searchResult.CustomName);
                var row = Int32.Parse(match.Groups[1].Captures[0].Value);
                var column = Int32.Parse(match.Groups[2].Captures[0].Value);
                if (display[row, column] == null) {
                    display[row, column] = new List<IMyLightingBlock>();
                }

                display[row, column].Add(searchResult);
            });
            return display;
        }

        enum EffectType {
            Starburst
        }

        List<IMyLightingBlock>[,] _display;

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            _display = BuildDisplay(GridTerminalSystem);
        }

        interface Effect {
            void Iterate(List<IMyLightingBlock>[,] display);
        }

        class StarburstEffect : Effect {
            Random _random = new Random();
            long _lastIteratedAt = CurrentTimeMillis();
            List<Color> _colors;
            long _millisPerProgress;
            double _density;

            public StarburstEffect(GetConfigurationProperty getConfigurationProperty) {
                var colors = getConfigurationProperty("colors").ToString().Split('\n');
                _colors = colors.Select(colorString => {
                    var color = colorString.Split(',');
                    return new Color(
                        Int32.Parse(color[0]),
                        Int32.Parse(color[1]),
                        Int32.Parse(color[2])
                    );
                }).ToList();
                _millisPerProgress = getConfigurationProperty("millisPerProgress").ToInt64();
                _density = getConfigurationProperty("density").ToDouble();
            }

            public void Iterate(List<IMyLightingBlock>[,] display) {
                var timeNow = CurrentTimeMillis();
                if (timeNow - _lastIteratedAt < _millisPerProgress) return;
                _lastIteratedAt = timeNow;

                foreach (var columns in display) {
                    foreach (var blocks in display) {
                        var shouldLightUp = _random.NextDouble() <= _density;
                        if (shouldLightUp) {
                            var color = _colors[_random.Next(_colors.Count)];
                            foreach (var block in blocks) {
                                block.Color = color;
                            }
                        }
                        else {
                            foreach (var block in blocks) {
                                block.Color = new Color(
                                    block.Color.R == 0 ? 0 : block.Color.R - 1,
                                    block.Color.G == 0 ? 0 : block.Color.G - 1,
                                    block.Color.B == 0 ? 0 : block.Color.B - 1
                                );
                            }
                        }
                    }
                }
            }
        }

        delegate MyIniValue GetConfigurationProperty(string propertyName);

        Effect _currentEffect;

        public void Main(string effectName, UpdateType updateSource) {
            if (effectName != "") {
                MyIni configuration = new MyIni();
                if (!configuration.TryParse(Me.CustomData)) {
                    Echo("Failed to parse configuration, not in INI format!");
                    return;
                }

                Storage = effectName;
                var effectType = GetEnumFromString<EffectType>(configuration.Get(effectName, "effect").ToString());

                if (effectType == EffectType.Starburst) {
                    _currentEffect = new StarburstEffect(propertyName =>
                        configuration.Get(effectName, propertyName)
                    );
                }

                foreach (var columns in _display) {
                    foreach (var block in columns) {
                        block.Color = Color.Black;
                    }
                }

                Echo("Using effect " + effectName + ".");
            }
            else if (_currentEffect != null) {
                _currentEffect.Iterate(_display);
            }
            else {
                Echo("No effect configured.");
            }
        }
    }
}