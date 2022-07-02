using System;
using System.Collections.Generic;
using System.Linq;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Text;
using VRageMath;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace UnHingedIndustries.uhiANIM {
    public sealed class Program : MyGridProgram {
        const string ScriptVersion = "2.0.4";
        const string WorkshopItemId = "2825279640";

        public static class Utils {
            public static string[] GetStepParts(string serializedStep, int requiredCount) {
                var stepParts = serializedStep.Split(';');
                if (stepParts.Length != requiredCount) {
                    throw new ArgumentException("step requires " + requiredCount + " arguments, received " + stepParts.Length + " in " + serializedStep);
                }
                else {
                    return stepParts;
                }
            }

            public static T GetEnumFromString<T>(string value) where T : struct {
                T result;
                if (Enum.TryParse(value, true, out result)) {
                    return result;
                }
                else {
                    throw new ArgumentException("Invalid value '" + value + "' for enum " + nameof(T));
                }
            }

            public static List<T> FindBlocks<T>(IMyGridTerminalSystem gridTerminalSystem, ComponentSearchType searchType, string searchString) where T : class, IMyTerminalBlock {
                var result = new List<T>();
                if (searchType == ComponentSearchType.Block) {
                    var foundBlock = gridTerminalSystem.GetBlockWithName(searchString);
                    if (foundBlock is T) {
                        result.Add(foundBlock as T);
                    }
                }
                else if (searchType == ComponentSearchType.Blocks) {
                    var searchResults = new List<IMyTerminalBlock>();
                    gridTerminalSystem.SearchBlocksOfName(searchString, searchResults, searchResult => searchResult is T);
                    searchResults.ForEach(searchResult => result.Add(searchResult as T));
                }
                else if (searchType == ComponentSearchType.Group) {
                    var group = gridTerminalSystem.GetBlockGroupWithName(searchString);
                    group?.GetBlocksOfType(result);
                }

                return result;
            }

            public static List<T> FindBlocksOfType<T>(IMyGridTerminalSystem gridTerminalSystem) where T : class {
                var result = new List<T>();
                gridTerminalSystem.GetBlocksOfType(result);
                return result;
            }
        }

        static IMechanicalBlockWrapper Wrap(IMyMechanicalConnectionBlock mechanicalBlock) {
            if (mechanicalBlock is IMyMotorStator) {
                return new MotorWrapper((IMyMotorStator)mechanicalBlock);
            }

            if (mechanicalBlock is IMyPistonBase) {
                return new PistonWrapper((IMyPistonBase)mechanicalBlock);
            }

            return new UnsupportedWrapper();
        }

        interface IMechanicalBlockWrapper {
            string Name { get; }
            float Value { get; }
            float LowerLimit { set; }
            float UpperLimit { set; }
            float Velocity { get; set; }
        }

        class MotorWrapper : IMechanicalBlockWrapper {
            IMyMotorStator _delegate;

            public MotorWrapper(IMyMotorStator @delegate) {
                _delegate = @delegate;
            }

            public string Name => _delegate.CustomName;

            public float Value => (float)((_delegate.Angle * 180) / Math.PI);

            public float Velocity {
                get { return _delegate.TargetVelocityRPM; }
                set { _delegate.TargetVelocityRPM = value; }
            }

            public float LowerLimit {
                set { _delegate.LowerLimitDeg = value; }
            }

            public float UpperLimit {
                set { _delegate.UpperLimitDeg = value; }
            }
        }

        class PistonWrapper : IMechanicalBlockWrapper {
            public IMyPistonBase Delegate;

            public PistonWrapper(IMyPistonBase @delegate) {
                Delegate = @delegate;
            }

            public string Name => Delegate.CustomName;
            public float Value => Delegate.CurrentPosition;

            public float Velocity {
                get { return Delegate.Velocity; }
                set { Delegate.Velocity = value; }
            }

            public float LowerLimit {
                set { Delegate.MinLimit = value; }
            }

            public float UpperLimit {
                set { Delegate.MaxLimit = value; }
            }
        }

        class UnsupportedWrapper : IMechanicalBlockWrapper {
            public string Name => "Unsupported block type!";
            public float Value => 0f;

            public float LowerLimit {
                set { }
            }

            public float UpperLimit {
                set { }
            }

            public float Velocity {
                get { return 0f; }
                set { }
            }
        }

        Animation _animation;

        class Animation {
            public readonly Dictionary<String, AnimationSegment> SegmentNamesToSegments;

            public Animation(IMyProgrammableBlock thisProgrammableBlock, IMyGridTerminalSystem gridTerminalSystem) {
                var deserializedValue = new MyIni();
                deserializedValue.TryParse(thisProgrammableBlock.CustomData);

                var animationDefiningBlocks = deserializedValue.Get("animation", "definitions")
                                                               .ToString()
                                                               .Split('\n')
                                                               .SelectMany(animationDefiningBlockName =>
                                                                   Utils.FindBlocks<IMyTerminalBlock>(
                                                                       gridTerminalSystem,
                                                                       ComponentSearchType.Block,
                                                                       animationDefiningBlockName
                                                                   )
                                                               );

                SegmentNamesToSegments
                    = animationDefiningBlocks.Concat(Enumerable.Repeat(thisProgrammableBlock, 1))
                                             .Distinct()
                                             .Select(block => block.CustomData)
                                             .Select(serializedBlockValue => {
                                                 var deserializedBlockValue = new MyIni();
                                                 deserializedBlockValue.TryParse(serializedBlockValue);
                                                 return deserializedBlockValue;
                                             }).SelectMany(deserializedBlockValue => {
                                                 var sectionNames = new List<string>();
                                                 deserializedBlockValue.GetSections(sectionNames);
                                                 return sectionNames.Where(sectionName => sectionName != "animation")
                                                                    .Select(segmentAndModeName => segmentAndModeName.Split('.'))
                                                                    .GroupBy(segmentAndModeName => segmentAndModeName[0])
                                                                    .Select(segmentNameToSegmentAndModeName => {
                                                                        return CreateSegment(
                                                                            deserializedBlockValue,
                                                                            segmentNameToSegmentAndModeName.Key,
                                                                            segmentNameToSegmentAndModeName.Select(it => it[1]).ToList(),
                                                                            gridTerminalSystem
                                                                        );
                                                                    });
                                             }).ToDictionary(segment => segment.Name);
            }

            AnimationSegment CreateSegment(MyIni deserializedValue, string segmentName, List<string> modeNames, IMyGridTerminalSystem gridTerminalSystem) {
                var modeNamesToModes = modeNames.Select(modeName =>
                    CreateMode(deserializedValue, segmentName, modeName, gridTerminalSystem)
                ).ToDictionary(mode => mode.Name);

                return new AnimationSegment(
                    segmentName,
                    modeNamesToModes
                );
            }

            AnimationSegmentMode CreateMode(MyIni deserializedValue, string segmentName, string modeName, IMyGridTerminalSystem gridTerminalSystem) {
                var sectionName = segmentName + '.' + modeName;

                var triggers = deserializedValue.Get(sectionName, "triggers")
                                                .ToString()
                                                .Split(',')
                                                .Where(trigger => trigger.Length != 0)
                                                .ToList();
                var repeat = deserializedValue.Get(sectionName, "repeat").ToBoolean();
                var steps = CreateSteps(deserializedValue.Get(sectionName, "steps"), gridTerminalSystem);

                return new AnimationSegmentMode(
                    modeName,
                    triggers,
                    repeat,
                    steps
                );
            }

            List<IAnimationStep> CreateSteps(MyIniValue serializedSteps, IMyGridTerminalSystem gridTerminalSystem) {
                IAnimationStep previousStep = null;
                return serializedSteps.ToString()
                                      .Split('\n')
                                      .Where(serializedStep => !serializedStep.StartsWith("-"))
                                      .Select(serializedStep => {
                                          var stepType = Utils.GetEnumFromString<AnimationStepType>(
                                              serializedStep.Split(';')[0]
                                          );

                                          var currentStep = CreateStep(
                                              stepType,
                                              serializedStep,
                                              gridTerminalSystem,
                                              previousStep
                                          );

                                          previousStep = currentStep;
                                          return currentStep;
                                      })
                                      .ToList();
            }

            IAnimationStep CreateStep(
                AnimationStepType stepType,
                string serializedStep,
                IMyGridTerminalSystem gridTerminalSystem,
                IAnimationStep previousStep
            ) {
                if (stepType == AnimationStepType.Move) {
                    return new MoveAnimationStep(
                        serializedStep,
                        gridTerminalSystem,
                        previousStep
                    );
                }

                if (stepType == AnimationStepType.Toggle) {
                    return new ToggleAnimationStep(
                        serializedStep,
                        gridTerminalSystem
                    );
                }

                if (stepType == AnimationStepType.Lock) {
                    return new LockAnimationStep(
                        serializedStep,
                        gridTerminalSystem
                    );
                }

                if (stepType == AnimationStepType.Trigger) {
                    return new TriggerAnimationStep(serializedStep);
                }

                throw new ArgumentException("invalid step type " + serializedStep);
            }
        }

        /**
        * Describes animation of a segment of the animatronic, e.g. legs.
        */
        class AnimationSegment {
            public readonly string Name;
            public readonly Dictionary<String, AnimationSegmentMode> ModeNameToMode;

            public AnimationSegment(string name, Dictionary<string, AnimationSegmentMode> modeNameToMode) {
                this.Name = name;
                this.ModeNameToMode = modeNameToMode;
            }
        }

        /**
        * Describes a particular mode of segment's animation, e.g. legs moving forward.
        */
        class AnimationSegmentMode {
            public readonly string Name;
            public readonly List<string> Triggers;

            public readonly bool Repeat;

            public readonly List<IAnimationStep> Steps;

            public AnimationSegmentMode(string name, List<string> triggers, bool repeat, List<IAnimationStep> steps) {
                this.Name = name;
                this.Triggers = triggers;
                this.Repeat = repeat;
                this.Steps = steps;
            }
        }

        /**
        * Describes a single step of segment's animation mode, e.g. raising legs.
        */
        interface IAnimationStep {
            bool IsCompleted();
            void Trigger(Dictionary<AnimationSegment, AnimationSegmentProgress> segmentsProgress);
        }

        class ToggleAnimationStep : IAnimationStep {
            readonly List<IMyFunctionalBlock> _components;
            readonly bool _enable;

            public ToggleAnimationStep(string serializedStep, IMyGridTerminalSystem gridTerminalSystem) {
                var stepParts = Utils.GetStepParts(serializedStep, 4);

                _components = Utils.FindBlocks<IMyFunctionalBlock>(
                    gridTerminalSystem,
                    Utils.GetEnumFromString<ComponentSearchType>(stepParts[1]),
                    stepParts[2]
                );
                _enable = "true".Equals(stepParts[3], StringComparison.OrdinalIgnoreCase);
            }

            public bool IsCompleted() {
                return true;
            }

            public void Trigger(Dictionary<AnimationSegment, AnimationSegmentProgress> segmentsProgress) {
                _components.ForEach(component => component.Enabled = _enable);
            }
        }

        class LockAnimationStep : IAnimationStep {
            readonly List<IMyLandingGear> _components;
            readonly bool _setToLock;
            readonly AnimationStepContinuityType _continuityType;

            public LockAnimationStep(string serializedStep, IMyGridTerminalSystem gridTerminalSystem) {
                var stepParts = Utils.GetStepParts(serializedStep, 5);

                _components = Utils.FindBlocks<IMyLandingGear>(
                    gridTerminalSystem,
                    Utils.GetEnumFromString<ComponentSearchType>(stepParts[1]),
                    stepParts[2]
                );
                _setToLock = "true".Equals(stepParts[3], StringComparison.OrdinalIgnoreCase);
                _continuityType = Utils.GetEnumFromString<AnimationStepContinuityType>(stepParts[4]);
            }

            public bool IsCompleted() {
                if (_continuityType == AnimationStepContinuityType.Wait) {
                    return _setToLock
                        ? _components.Any(component => component.LockMode == LandingGearMode.Locked)
                        : _components.TrueForAll(component => component.LockMode != LandingGearMode.Locked);
                }

                return true;
            }

            public void Trigger(Dictionary<AnimationSegment, AnimationSegmentProgress> segmentsProgress) {
                _components.ForEach(component => {
                    component.Enabled = true;
                    component.AutoLock = _setToLock;
                    if (_setToLock) {
                        component.Lock();
                    }
                    else {
                        component.Unlock();
                    }
                });
            }
        }

        class TriggerAnimationStep : IAnimationStep {
            AnimationSegment _segment;
            readonly string _segmentName;
            readonly string _modeName;

            public TriggerAnimationStep(string serializedStep) {
                var stepParts = Utils.GetStepParts(serializedStep, 3);
                _segmentName = stepParts[1];
                _modeName = stepParts[2];
            }

            public bool IsCompleted() {
                return true;
            }

            public void Trigger(Dictionary<AnimationSegment, AnimationSegmentProgress> segmentsProgress) {
                if (_segment == null) {
                    _segment = segmentsProgress.Keys.FirstOrDefault(it => it.Name == _segmentName);
                }

                if (_segment != null) {
                    var progress = segmentsProgress[_segment];
                    progress.ActiveMode = _segment.ModeNameToMode[_modeName];
                    progress.ActiveStepId = 0;
                }
            }
        }

        class MoveAnimationStep : IAnimationStep {
            readonly List<IMechanicalBlockWrapper> _components;
            readonly float _targetValue;
            readonly float _precision;
            readonly float _velocity;
            readonly AnimationStepContinuityType _continuityType;

            public MoveAnimationStep(string serializedStep, IMyGridTerminalSystem gridTerminalSystem, IAnimationStep previousStep) {
                var stepParts = Utils.GetStepParts(serializedStep, 7);

                var previousMoveStep = previousStep is MoveAnimationStep ? previousStep as MoveAnimationStep : null;
                if (stepParts.Any(stepPart => stepPart.Length == 0) && previousMoveStep == null) {
                    throw new ArgumentException("no previous value to fill in to " + serializedStep);
                }

                _components = stepParts[2].Length != 0
                    ? Utils.FindBlocks<IMyMechanicalConnectionBlock>(
                        gridTerminalSystem,
                        Utils.GetEnumFromString<ComponentSearchType>(stepParts[1]),
                        stepParts[2]
                    ).Select(Wrap).ToList()
                    : previousMoveStep._components;
                _targetValue = stepParts[3].Length != 0 ? float.Parse(stepParts[3]) : previousMoveStep._targetValue;
                _precision = stepParts[4].Length != 0 ? float.Parse(stepParts[4]) : previousMoveStep._precision;
                _velocity = stepParts[5].Length != 0 ? float.Parse(stepParts[5]) : previousMoveStep._velocity;
                _continuityType = stepParts[6].Length != 0 ? Utils.GetEnumFromString<AnimationStepContinuityType>(stepParts[6]) : previousMoveStep._continuityType;
            }

            public bool IsCompleted() {
                if (_continuityType == AnimationStepContinuityType.Wait) {
                    foreach (var component in _components) {
                        var componentCurrentValue = component.Value;
                        if (componentCurrentValue < _targetValue - _precision || componentCurrentValue > _targetValue + _precision) {
                            return false;
                        }
                    }
                }

                return true;
            }

            public void Trigger(Dictionary<AnimationSegment, AnimationSegmentProgress> segmentsProgress) {
                foreach (var component in _components) {
                    var currentValue = component.Value;
                    if (currentValue < _targetValue) {
                        component.LowerLimit = currentValue;
                        component.UpperLimit = _targetValue;
                        component.Velocity = Math.Abs(_velocity);
                    }
                    else {
                        component.LowerLimit = _targetValue;
                        component.UpperLimit = currentValue;
                        component.Velocity = -Math.Abs(_velocity);
                    }
                }
            }
        }

        enum AnimationStepContinuityType {
            Continue,
            Wait
        }

        public enum ComponentSearchType {
            Block,
            Blocks,
            Group
        }

        enum AnimationStepType {
            Move,
            Lock,
            Toggle,
            Trigger
        }

        IMyShipController _foundController;

        IMyShipController GetController() {
            if (_foundController == null) {
                var controllers = new List<IMyShipController>();
                GridTerminalSystem.GetBlocksOfType(controllers);
                _foundController = controllers.Where(it => it.CubeGrid == Me.CubeGrid)
                                              .Where(it => it.IsMainCockpit)
                                              .DefaultIfEmpty(controllers.FirstOrDefault())
                                              .First();
            }

            return _foundController;
        }

        class AnimationSegmentProgress {
            public AnimationSegmentMode ActiveMode;
            public int ActiveStepId = -1;
        }

        delegate bool ShouldTrigger(string argument, Vector3 moveIndicator);

        delegate void EvaluateTrigger(string argument, Vector3 moveIndicator);

        static ShouldTrigger TranslateTrigger(string trigger) {
            if (trigger.StartsWith("ARGUMENT_")) {
                return (argument, moveIndicator) => argument == trigger.Remove(0, 9);
            }

            switch (trigger) {
                case "NONE": return (argument, moveIndicator) => false;
                case "CONTROLLER_NO_INPUT": return (argument, moveIndicator) => moveIndicator.X == 0 && moveIndicator.Y == 0 && moveIndicator.Z == 0;
                case "CONTROLLER_FORWARD": return (argument, moveIndicator) => moveIndicator.Z < 0;
                case "CONTROLLER_BACKWARD": return (argument, moveIndicator) => moveIndicator.Z > 0;
                case "CONTROLLER_NEITHER_FORWARD_NOR_BACKWARD": return (argument, moveIndicator) => moveIndicator.Z == 0;
                case "CONTROLLER_LEFT": return (argument, moveIndicator) => moveIndicator.X < 0;
                case "CONTROLLER_RIGHT": return (argument, moveIndicator) => moveIndicator.X > 0;
                case "CONTROLLER_NEITHER_LEFT_NOR_RIGHT": return (argument, moveIndicator) => moveIndicator.X == 0;
                case "CONTROLLER_UP": return (argument, moveIndicator) => moveIndicator.Y > 0;
                case "CONTROLLER_DOWN": return (argument, moveIndicator) => moveIndicator.Y < 0;
                case "CONTROLLER_NEITHER_UP_NOR_DOWN": return (argument, moveIndicator) => moveIndicator.Y == 0;
                default: throw new Exception("unknown trigger: " + trigger);
            }
        }

        Dictionary<AnimationSegment, AnimationSegmentProgress> _segmentsProgress;
        List<EvaluateTrigger> _triggerEvaluations;

        void SetupAnimation() {
            Echo("Setting up animation...");
            _animation = new Animation(Me, GridTerminalSystem);

            _segmentsProgress = _animation.SegmentNamesToSegments.Values.ToDictionary(segment => segment, segment => new AnimationSegmentProgress());

            _triggerEvaluations = _animation.SegmentNamesToSegments.Values
                                            .SelectMany(segment =>
                                                segment.ModeNameToMode.Values.Select(mode => {
                                                    var progress = _segmentsProgress[segment];
                                                    var triggers = mode.Triggers
                                                                       .Select(TranslateTrigger);

                                                    return new EvaluateTrigger((argument, moveIndicator) => {
                                                        if (progress.ActiveMode != mode
                                                            && triggers.All(trigger => trigger(argument, moveIndicator))) {
                                                            progress.ActiveMode = mode;
                                                            progress.ActiveStepId = -1;
                                                        }
                                                    });
                                                })
                                            ).ToList();

            Echo("Animation setup completed.");
        }

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SetupAnimation();
        }

        class AnimationModeRecorder {
            class FunctionalBlockState {
                public bool Enabled;

                public FunctionalBlockState(bool enabled) {
                    Enabled = enabled;
                }
            }

            class LandingGearState {
                public bool LockEnabled;

                public LandingGearState(bool lockEnabled) {
                    LockEnabled = lockEnabled;
                }
            }

            class MechanicalBlockState {
                public float Value;
                public float Velocity;

                public MechanicalBlockState(float value, float velocity) {
                    Value = value;
                    Velocity = velocity;
                }
            }

            public string StageName;
            public string ModeName;
            Dictionary<IMyFunctionalBlock, FunctionalBlockState> _functionalBlockStates;
            Dictionary<IMyLandingGear, LandingGearState> _landingGearStates;
            Dictionary<IMechanicalBlockWrapper, MechanicalBlockState> _mechanicalBlockStates;
            public List<string> RecordedSteps = new List<string>();

            public AnimationModeRecorder(string stageName, string modeName, IMyGridTerminalSystem gridTerminalSystem) {
                StageName = stageName;
                ModeName = modeName;

                _functionalBlockStates = Utils.FindBlocksOfType<IMyFunctionalBlock>(gridTerminalSystem)
                                              .ToDictionary(
                                                  block => block,
                                                  block => new FunctionalBlockState(block.Enabled)
                                              );

                _landingGearStates = Utils.FindBlocksOfType<IMyLandingGear>(gridTerminalSystem)
                                          .ToDictionary(
                                              block => block,
                                              block => new LandingGearState(block.AutoLock)
                                          );

                _mechanicalBlockStates = Utils.FindBlocksOfType<IMyMechanicalConnectionBlock>(gridTerminalSystem)
                                              .Select(Wrap)
                                              .ToDictionary(
                                                  block => block,
                                                  block => new MechanicalBlockState(block.Value, block.Velocity)
                                              );
            }

            public void RecordSteps() {
                foreach (var blockToState in _functionalBlockStates) {
                    var block = blockToState.Key;
                    var state = blockToState.Value;
                    if (block.Enabled != state.Enabled) {
                        state.Enabled = block.Enabled;
                        RecordedSteps.Add(
                            AnimationStepType.Toggle + ";" +
                            ComponentSearchType.Block + ';' + block.CustomName + ';' +
                            block.Enabled
                        );
                    }
                }

                foreach (var blockToState in _landingGearStates) {
                    var block = blockToState.Key;
                    var state = blockToState.Value;
                    if (block.AutoLock != state.LockEnabled) {
                        state.LockEnabled = block.AutoLock;
                        RecordedSteps.Add(
                            AnimationStepType.Lock + ";" +
                            ComponentSearchType.Block + ';' + block.CustomName + ';' +
                            block.AutoLock + ';' + AnimationStepContinuityType.Continue
                        );
                    }
                }


                foreach (var blockToState in _mechanicalBlockStates) {
                    var block = blockToState.Key;
                    var state = blockToState.Value;
                    var currentValue = block.Value;
                    var currentVelocity = block.Velocity;
                    if (Math.Abs(currentValue - state.Value) > 0.1f || Math.Abs(currentVelocity - state.Velocity) > 0) {
                        state.Value = currentValue;
                        state.Velocity = currentVelocity;
                        RecordedSteps.Add(
                            AnimationStepType.Move + ";" +
                            ComponentSearchType.Block + ';' + block.Name + ';' +
                            currentValue + ";0.1;" + Math.Abs(currentVelocity) + ';' +
                            AnimationStepContinuityType.Continue
                        );
                    }
                }

                if (RecordedSteps.Count != 0) {
                    var lastStep = RecordedSteps[RecordedSteps.Count - 1];
                    RecordedSteps[RecordedSteps.Count - 1] = lastStep.Replace(";" + AnimationStepContinuityType.Continue, ";" + AnimationStepContinuityType.Wait);
                }
            }

            public void Write(IMyProgrammableBlock me) {
                var deserializedValue = new MyIni();
                deserializedValue.TryParse(me.CustomData);

                var section = StageName + '.' + ModeName;
                deserializedValue.Set(section, "steps", string.Join("\n", RecordedSteps));
                if (deserializedValue.Get(section, "triggers").ToString().Length == 0) {
                    deserializedValue.Set(section, "triggers", "NONE");
                }

                if (deserializedValue.Get(section, "repeat").ToString().Length == 0) {
                    deserializedValue.Set(section, "repeat", "false");
                }

                me.CustomData = deserializedValue.ToString();
            }
        }

        AnimationModeRecorder _animationModeRecorder;

        public void Main(string argument, UpdateType updateSource) {
            var screenTextBuilder = new StringBuilder();
            screenTextBuilder.Append("UnHinged Industries ANIM System\nVersion " + ScriptVersion + "\n\n");

            if (argument == "SETUP") {
                SetupAnimation();
            }

            if (argument.StartsWith("RECORD;")) {
                var argumentParts = argument.Split(';');
                if (argumentParts.Length != 3) {
                    Echo("Animation recording mode argument must contain stage and mode, e.g. RECORD;legs;moveForward");
                    return;
                }

                if (_animationModeRecorder != null) {
                    Echo("Already recording, please call RECORDING_DONE if finished or RECORD_STEPS to snapshot current changes.");
                    return;
                }

                var stageName = argumentParts[1];
                var modeName = argumentParts[2];
                _animationModeRecorder = new AnimationModeRecorder(stageName, modeName, GridTerminalSystem);
            }

            if (_animationModeRecorder != null) {
                screenTextBuilder.Append("Recording steps for:\n Stage: " + _animationModeRecorder.StageName + "\n Mode: " + _animationModeRecorder.ModeName + '\n');
                screenTextBuilder.Append(" Current recorded steps count: " + _animationModeRecorder.RecordedSteps.Count);
                Me.GetSurface(0).WriteText(screenTextBuilder.ToString());
                switch (argument) {
                    case "RECORD_STEPS":
                        _animationModeRecorder.RecordSteps();
                        return;
                    case "RECORDING_DONE":
                        _animationModeRecorder.Write(Me);
                        _animationModeRecorder = null;
                        SetupAnimation();
                        break;
                    default:
                        return; // do not proceed with animation while recording
                }
            }

            var controller = GetController();

            // Change animation modes
            _triggerEvaluations.ForEach(triggerEvaluation => triggerEvaluation(argument, controller.MoveIndicator));

            if (_segmentsProgress.Count == 0) {
                screenTextBuilder.Append("No animation segments are configured.");
            }
            else {
                screenTextBuilder.Append("Active animation steps:\n");
            }

            // Continue animation
            foreach (var segmentAndProgress in _segmentsProgress) {
                var segment = segmentAndProgress.Key;
                var progress = segmentAndProgress.Value;
                var mode = progress.ActiveMode;
                if (mode != null) {
                    if (progress.ActiveStepId != -1) {
                        var previousStep = mode.Steps[progress.ActiveStepId];

                        // wait for previous step to finish
                        if (!previousStep.IsCompleted()) {
                            return;
                        }
                    }

                    // go to next animation step
                    var doTrigger = true;
                    if (progress.ActiveStepId == mode.Steps.Count - 1) {
                        if (mode.Repeat == false) {
                            doTrigger = false;
                        }
                        else {
                            progress.ActiveStepId = 0;
                        }
                    }
                    else {
                        progress.ActiveStepId += 1;
                    }

                    screenTextBuilder.Append(" > " + segment.Name + "." + mode.Name + "." + progress.ActiveStepId + '\n');
                    var activeStep = mode.Steps[progress.ActiveStepId];

                    if (doTrigger) activeStep.Trigger(_segmentsProgress);
                }
            }

            Me.GetSurface(0).WriteText(screenTextBuilder.ToString());
        }
    }
}