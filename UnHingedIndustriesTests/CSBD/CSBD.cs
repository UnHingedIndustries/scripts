using Moq;
using NUnit.Framework;
using Sandbox.ModAPI.Ingame;
using UnHingedIndustries.CSBD;

namespace UnHingedIndustriesTests.CSBD {
    public class Tests {
        [Test]
        public void TestMain() {
            var program = new Program();
            var gridTerminalSystemMock = new Mock<IMyGridTerminalSystem>();
            program.SetPrivatePropertyValue("GridTerminalSystem", gridTerminalSystemMock.Object);
            program.Main("test", UpdateType.Trigger);
        }
    }
}