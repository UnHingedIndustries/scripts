using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
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
        const string WorkshopItemId = "2838469555";
        const string ModIoItemId = "2224751";

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

        static List<Color> ParseColors(string colors, char separator) {
            return colors.Split(separator).Select(ParseColor).ToList();
        }

        static Color ParseColor(string colorString) {
            var color = colorString.Split(',');
            return new Color(
                Int32.Parse(color[0]),
                Int32.Parse(color[1]),
                Int32.Parse(color[2])
            );
        }

        Display BuildDisplay(IMyGridTerminalSystem gridTerminalSystem) {
            var rgbLightNameRegex = new System.Text.RegularExpressions.Regex(
                @"\bRGB\:([0-9]+):([0-9]+)(\:([A-z,]*))?\b",
                System.Text.RegularExpressions.RegexOptions.Compiled
            );

            var searchResults = new List<IMyLightingBlock>();
            var maxX = 0;
            var maxY = 0;
            gridTerminalSystem.GetBlocksOfType(searchResults, searchResult => {
                var match = rgbLightNameRegex.Match(searchResult.CustomName);
                if (!match.Success) return false;
                var x = Int32.Parse(match.Groups[1].Captures[0].Value);
                if (x > maxX) maxX = x;
                var y = Int32.Parse(match.Groups[2].Captures[0].Value);
                if (y > maxY) maxY = y;
                return true;
            });

            var display = new List<RGBBlock>[maxX + 1, maxY + 1];
            for (int x = 0; x < display.GetLength(0); ++x) {
                for (int y = 0; y < display.GetLength(1); ++y) {
                    display[x, y] = new List<RGBBlock>();
                }
            }

            searchResults.ForEach(searchResult => {
                var match = rgbLightNameRegex.Match(searchResult.CustomName);
                var x = Int32.Parse(match.Groups[1].Captures[0].Value);
                var y = Int32.Parse(match.Groups[2].Captures[0].Value);
                var sections = match.Groups[4].Captures.Count == 1
                    ? match.Groups[4].Captures[0].Value.Split(',').ToImmutableHashSet()
                    : ImmutableHashSet<string>.Empty;
                display[x, y].Add(new RGBBlock(sections, searchResult));
            });

            return new Display(display);
        }

        enum EffectType {
            Starburst,
            Fade,
            Rolling
        }

        Display _display;

        interface Effect {
            void Iterate(Display display);
        }

        class TimedEffect : Effect {
            long _lastIteratedAt = CurrentTimeMillis();
            readonly long _millisPerIteration;
            readonly Effect _effect;

            public TimedEffect(GetConfigurationProperty getConfigurationProperty, Effect effect) {
                _millisPerIteration = getConfigurationProperty("millisPerIteration").ToInt64();
                _effect = effect;
            }

            public void Iterate(Display display) {
                if (_millisPerIteration != 0) {
                    var timeNow = CurrentTimeMillis();
                    if (timeNow - _lastIteratedAt < _millisPerIteration) return;
                    _lastIteratedAt = timeNow;
                }

                _effect.Iterate(display);
            }
        }

        class RGBBlock {
            public readonly ImmutableHashSet<string> Sections;
            readonly IMyLightingBlock _block;

            public RGBBlock(ImmutableHashSet<string> sections, IMyLightingBlock block) {
                Sections = sections;
                _block = block;
            }

            public Color Color {
                get { return _block.Color; }
                set { _block.Color = value; }
            }
        }

        class Display {
            readonly List<RGBBlock>[,] _blocks;

            public Display(ImmutableHashSet<string> sections, Display other) {
                var blocks = other._blocks;
                _blocks = new List<RGBBlock>[blocks.GetLength(0), blocks.GetLength(1)];
                bool allBlocksMatching = true;

                for (int x = 0; x < blocks.GetLength(0); ++x) {
                    for (int y = 0; y < blocks.GetLength(1); ++y) {
                        List<RGBBlock> newBlocks = new List<RGBBlock>();
                        foreach (var block in blocks[x, y]) {
                            if ((sections.IsEmpty && block.Sections.IsEmpty) || sections.Overlaps(block.Sections)) {
                                newBlocks.Add(block);
                            }
                            else {
                                allBlocksMatching = false;
                            }
                        }

                        _blocks[x, y] = newBlocks;
                    }
                }

                if (allBlocksMatching) {
                    _blocks = blocks;
                }
            }

            public Display(List<RGBBlock>[,] blocks) {
                _blocks = blocks;
            }

            public int AllBlocksCount => _blocks.Length;

            public int GetLength(int dimension) => _blocks.GetLength(dimension);

            public List<RGBBlock> this[int x, int y] => _blocks[x, y];
        }

        class SectionedEffect : Effect {
            readonly Effect _effect;
            readonly Display _display;

            public SectionedEffect(Display display, GetConfigurationProperty getConfigurationProperty, Effect effect) {
                _effect = effect;
                var sectionsProperty = getConfigurationProperty("sections");
                var sections = sectionsProperty.IsEmpty
                    ? ImmutableHashSet<string>.Empty
                    : sectionsProperty
                      .ToString()
                      .Split('\n')
                      .ToImmutableHashSet();
                _display = new Display(sections, display);
            }

            public void Iterate(Display display) {
                _effect.Iterate(_display);
            }
        }

        class StarburstEffect : Effect {
            static int _randomSeed = int.MinValue;
            readonly Random _random = new Random(_randomSeed += 5);
            readonly List<Color> _colors;
            readonly int _blocksPerIteration;

            public StarburstEffect(GetConfigurationProperty getConfigurationProperty) {
                _colors = ParseColors(getConfigurationProperty("colors").ToString(), '\n');
                _blocksPerIteration = getConfigurationProperty("blocksPerIteration").ToInt16(1);
            }

            public void Iterate(Display display) {
                if (_blocksPerIteration < 1) return;
                foreach (var blockId in Enumerable.Range(0, _blocksPerIteration)) {
                    var x = _random.Next(display.GetLength(0));
                    var y = _random.Next(display.GetLength(1));
                    var color = _colors[_random.Next(_colors.Count)];
                    var blocks = display[x, y];
                    if (blocks.Count != 0) {
                        var block = blocks[_random.Next(blocks.Count)];
                        block.Color = color;
                    }
                }
            }
        }

        class FadeEffect : Effect {
            readonly Color _color;
            readonly int _increment;

            public FadeEffect(GetConfigurationProperty getConfigurationProperty) {
                _color = ParseColor(getConfigurationProperty("color").ToString());
                _increment = Math.Abs(getConfigurationProperty("increment").ToInt16(1));
            }

            public void Iterate(Display display) {
                for (int x = 0; x < display.GetLength(0); ++x) {
                    for (int y = 0; y < display.GetLength(1); ++y) {
                        foreach (var block in display[x, y]) {
                            if (block.Color != _color) {
                                block.Color = IterateColor(block.Color);
                            }
                        }
                    }
                }
            }

            Color IterateColor(Color color) {
                return new Color(
                    IterateColorComponent(_color.R, color.R),
                    IterateColorComponent(_color.G, color.G),
                    IterateColorComponent(_color.B, color.B)
                );
            }

            byte IterateColorComponent(byte targetComponent, byte currentComponent) {
                var newValue = currentComponent + Math.Sign(targetComponent - currentComponent) * _increment;
                if ((currentComponent < targetComponent && newValue > targetComponent)
                    || currentComponent > targetComponent && newValue < targetComponent) {
                    return targetComponent;
                }

                return (byte) newValue;
            }
        }

        class RollingEffect : Effect {
            enum Axis {
                Horizontal,
                Vertical
            }

            enum Direction {
                Positive,
                Negative
            }

            readonly int _movementDimension;
            readonly int _fillDimension;
            readonly List<Color> _colors;
            int _currentPosition;
            readonly int _positionIncrement;

            public RollingEffect(GetConfigurationProperty getConfigurationProperty) {
                _colors = ParseColors(getConfigurationProperty("colors").ToString(), '\n');
                var increment = getConfigurationProperty("increment").ToInt16(1);
                var axis = GetEnumFromString<Axis>(getConfigurationProperty("axis").ToString(Axis.Horizontal.ToString()));
                if (axis == Axis.Horizontal) {
                    _movementDimension = 1;
                    _fillDimension = 0;
                }
                else {
                    _movementDimension = 0;
                    _fillDimension = 1;
                }

                var direction = GetEnumFromString<Direction>(getConfigurationProperty("direction").ToString(Direction.Positive.ToString()));
                _currentPosition = getConfigurationProperty("offset").ToInt16(0);
                _positionIncrement = direction == Direction.Positive ? 1 * increment : -1 * increment;
            }


            public void Iterate(Display display) {
                var axisLength = display.GetLength(_movementDimension);
                var fillLength = display.GetLength(_fillDimension);
                if (_currentPosition >= axisLength) {
                    _currentPosition = _currentPosition % axisLength;
                }
                else if (_currentPosition < 0) {
                    _currentPosition = axisLength - Math.Abs(_currentPosition % axisLength);
                }

                var colorPosition = _currentPosition;
                var colorPositionIncrement = Math.Sign(_positionIncrement);
                if (colorPositionIncrement == 0) colorPositionIncrement = 1;
                _colors.ForEach(color => {
                    if (colorPosition >= axisLength) {
                        colorPosition = 0;
                    }
                    else if (colorPosition < 0) {
                        colorPosition = axisLength - 1;
                    }

                    for (int fill = 0; fill < fillLength; ++fill) {
                        display[
                            _movementDimension == 0 ? colorPosition : fill,
                            _fillDimension == 0 ? colorPosition : fill
                        ].ForEach(block => block.Color = color);
                    }

                    colorPosition += colorPositionIncrement;
                });
                _currentPosition += _positionIncrement;
            }
        }

        delegate MyIniValue GetConfigurationProperty(string propertyName);

        Dictionary<string, Effect> _activeEffects;

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            _display = BuildDisplay(GridTerminalSystem);
            _activeEffects = new Dictionary<string, Effect>();
            if (Storage != "") {
                foreach (var effectName in Storage.Split('\n')) {
                    Main(effectName);
                }
            }
        }

        public void Main(string argument) {
            if (argument != "") {
                if (argument == "CLEAR") {
                    _activeEffects.Clear();
                    return;
                }

                if (argument == "SETUP") {
                    _display = BuildDisplay(GridTerminalSystem);
                    _activeEffects.Clear();
                    return;
                }

                MyIni configuration = new MyIni();
                if (!configuration.TryParse(Me.CustomData)) {
                    Echo("Failed to parse configuration, not in INI format!");
                    return;
                }

                if (argument == "ALL") {
                    var allEffects = new List<string>();
                    configuration.GetSections(allEffects);
                    allEffects.ForEach(Main);
                    return;
                }

                var effectName = argument;

                var effectNameFromConfiguration = configuration.Get(effectName, "effect");
                if (effectNameFromConfiguration.IsEmpty) {
                    Echo("Undefined effect: " + effectName);
                    return;
                }

                var effectType = GetEnumFromString<EffectType>(effectNameFromConfiguration.ToString());

                GetConfigurationProperty getConfigurationProperty = propertyName => configuration.Get(argument, propertyName);
                Effect effect = null;
                if (effectType == EffectType.Starburst) {
                    effect = new StarburstEffect(getConfigurationProperty);
                }
                else if (effectType == EffectType.Fade) {
                    effect = new FadeEffect(getConfigurationProperty);
                }
                else if (effectType == EffectType.Rolling) {
                    effect = new RollingEffect(getConfigurationProperty);
                }

                if (effect == null) {
                    Echo("Effect " + argument + " does not exist!");
                }
                else {
                    _activeEffects[effectName] = new TimedEffect(
                        getConfigurationProperty,
                        new SectionedEffect(_display, getConfigurationProperty, effect)
                    );
                    Echo("Using effect " + effectName + ".");
                    Storage = string.Join("\n", _activeEffects.Keys);
                }
            }
            else if (_activeEffects.Count != 0) {
                foreach (var effectNameToEffect in _activeEffects) {
                    Echo("Iterating " + effectNameToEffect.Key);
                    effectNameToEffect.Value.Iterate(_display);
                }
            }
            else {
                Echo("No effect configured.");
            }
        }
    }
}