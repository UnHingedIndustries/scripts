using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using UnHingedIndustries.uhiANIM;
using VRageMath;
using static System.IO.File;

namespace UnHingedIndustriesTests.uhiANIM {
    public class AnimationTests {
        [Test]
        public void Constructor_WhenAnimationIsWellDefined_ShouldCreateAnimationWithAllSteps() {
            // given arguments
            var programmableBlockMock = new Mock<IMyProgrammableBlock>();
            var gridTerminalSystemMock = new Mock<IMyGridTerminalSystem>();

            programmableBlockMock.SetupGet(it => it.CustomData).Returns(
                ReadAllText("../../../uhiANIM/example-animation-all-features.ini")
            );

            // given animation defining blocks
            var someBlockMock = new Mock<IMyTerminalBlock>();
            var someOtherBlockMock = new Mock<IMyTerminalBlock>();
            gridTerminalSystemMock.Setup(it => it.GetBlockWithName("Some Block")).Returns(someBlockMock.Object);
            gridTerminalSystemMock.Setup(it => it.GetBlockWithName("Some Other Block")).Returns(someOtherBlockMock.Object);
            someBlockMock.SetupGet(it => it.CustomData).Returns(
                ReadAllText("../../../uhiANIM/example-animation-sub-definition.ini")
            );
            someOtherBlockMock.SetupGet(it => it.CustomData).Returns("");

            // given info surface block
            var infoSurfaceBlockMock = new Mock<IMyTextSurface>().As<IMyTerminalBlock>();
            gridTerminalSystemMock.Setup(it => it.GetBlockWithName("My Informative LCD")).Returns(infoSurfaceBlockMock.Object);

            // given ship controller block
            var shipControllerBlockMock = new Mock<IMyShipController>();
            gridTerminalSystemMock.Setup(it => it.GetBlockWithName("Pilot Seat")).Returns(shipControllerBlockMock.Object);

            // given step blocks
            var moveStepBlockName = "TEST_VALUE";
            var moveStepBlockMock = new Mock<IMyMechanicalConnectionBlock>();
            gridTerminalSystemMock.Setup(it => it.GetBlockWithName(moveStepBlockName)).Returns(moveStepBlockMock.Object);

            var shiftStepBlockName = "testCamelCaseValue=equalsShouldStillWork";
            var shiftStepBlockMock = new Mock<IMyMechanicalConnectionBlock>();
            gridTerminalSystemMock.Setup(it => it.GetBlockWithName(shiftStepBlockName)).Returns(shiftStepBlockMock.Object);

            var toggleStepBlockName = "Hazard Light";
            var toggleStepBlockMock = new Mock<IMyFunctionalBlock>();
            gridTerminalSystemMock.Setup(it => it.SearchBlocksOfName(
                toggleStepBlockName,
                It.IsAny<List<IMyTerminalBlock>>(),
                It.IsAny<Func<IMyTerminalBlock, bool>>())
            ).Callback((string name, List<IMyTerminalBlock> results, Func<IMyTerminalBlock, bool> filter) => results.Add(toggleStepBlockMock.Object));

            var lockStepBlockName = "Legs Magnetic Plates";
            var lockStepBlockMock = new Mock<IMyLandingGear>();
            var lockStepBlockGroupMock = new Mock<IMyBlockGroup>();
            gridTerminalSystemMock.Setup(it => it.GetBlockGroupWithName(lockStepBlockName)).Returns(lockStepBlockGroupMock.Object);
            lockStepBlockGroupMock.Setup(it => it.GetBlocksOfType(
                It.IsAny<List<IMyLandingGear>>(),
                null
            )).Callback((List<IMyLandingGear> result, Func<IMyLandingGear, bool> collect) => result.Add(lockStepBlockMock.Object));

            var includeStepBlockName = "Included Block";
            var includeStepBlockMock = new Mock<IMyMechanicalConnectionBlock>();
            gridTerminalSystemMock.Setup(it => it.GetBlockWithName(includeStepBlockName)).Returns(includeStepBlockMock.Object);

            // when
            var animation = new Program.Animation(programmableBlockMock.Object, gridTerminalSystemMock.Object);

            // then animation configuration is setup
            Assert.AreSame(infoSurfaceBlockMock.Object, animation.AnimationInfoSurface);
            Assert.AreSame(shipControllerBlockMock.Object, animation.AnimationController);
            Assert.AreEqual(25, animation.ControllerDeadzonePercentage);
            Assert.AreEqual(true, animation.AutomaticallyDetermineInputSensitivity);
            Assert.AreEqual(new Vector3(2, 2, 2), animation.MoveIndicatorSensitivity);
            Assert.AreEqual(new Vector2(18, 18), animation.RotationSensitivity);
            Assert.AreEqual(2, animation.RollSensitivity);

            // and animation mode is setup
            var exampleMode = animation.SegmentNamesToSegments["exampleSegment"].ModeNameToMode["exampleMode"];
            Assert.AreEqual(
                new List<string> {"CONTROLLER_UP", "CONTROLLER_ROLL_COUNTERCLOCKWISE"},
                exampleMode.Triggers
            );
            Assert.AreEqual(true, exampleMode.Repeat);
            Assert.AreEqual(5, exampleMode.Priority);

            // and animation steps are setup
            Assert.IsInstanceOf<Program.MoveAnimationStep>(exampleMode.Steps[0]);
            Assert.IsInstanceOf<Program.ShiftAnimationStep>(exampleMode.Steps[1]);
            Assert.IsInstanceOf<Program.ToggleAnimationStep>(exampleMode.Steps[2]);
            Assert.IsInstanceOf<Program.LockAnimationStep>(exampleMode.Steps[3]);
            Assert.IsInstanceOf<Program.TriggerAnimationStep>(exampleMode.Steps[4]);
            Assert.IsInstanceOf<Program.MoveAnimationStep>(exampleMode.Steps[5]);
            Assert.IsInstanceOf<Program.MoveAnimationStep>(exampleMode.Steps[6]);

            gridTerminalSystemMock.Verify(it => it.GetBlockWithName(moveStepBlockName), Times.Once);
            gridTerminalSystemMock.Verify(it => it.GetBlockWithName(shiftStepBlockName), Times.Once);
            gridTerminalSystemMock.Verify(it =>
                    it.SearchBlocksOfName(
                        toggleStepBlockName,
                        It.IsAny<List<IMyTerminalBlock>>(),
                        It.IsAny<Func<IMyTerminalBlock, bool>>()
                    ),
                Times.Once
            );
            gridTerminalSystemMock.Verify(it => it.GetBlockGroupWithName(lockStepBlockName), Times.Once);
            lockStepBlockGroupMock.Verify(it =>
                    it.GetBlocksOfType(
                        It.IsAny<List<IMyLandingGear>>(),
                        null
                    ),
                Times.Once
            );
            gridTerminalSystemMock.Verify(it => it.GetBlockWithName(includeStepBlockName), Times.Exactly(2));

            // and sub-definition mode is setup
            var subDefinitionMode = animation.SegmentNamesToSegments["someSubDefinitionSegment"].ModeNameToMode["someSubDefinitionMode"];
            Assert.AreEqual(
                new List<string> {"NONE"},
                subDefinitionMode.Triggers
            );
        }

        [Test]
        public void Constructor_WhenAnimationContainsCircularInclusions_ShouldThrow() {
            // given arguments
            var programmableBlockMock = new Mock<IMyProgrammableBlock>();
            var gridTerminalSystemMock = new Mock<IMyGridTerminalSystem>();

            programmableBlockMock.SetupGet(it => it.CustomData).Returns(
                ReadAllText("../../../uhiANIM/example-animation-circular-reference.ini")
            );

            // when
            var exception = Assert.Throws<ArgumentException>(() => { new Program.Animation(programmableBlockMock.Object, gridTerminalSystemMock.Object); });

            // then
            Assert.AreEqual(
                "circular inclusion is not allowed; started from exampleSegment.referenceStart (step INCLUDE;exampleSegment;referenceChain)",
                exception.Message
            );
        }
    }
}