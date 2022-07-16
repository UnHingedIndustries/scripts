using System;
using NUnit.Framework;
using UnHingedIndustries.uhiANIM;

namespace UnHingedIndustriesTests.uhiANIM {
    public class UtilsTests {
        [Test]
        public void GetStepParts_GivenInValidNumberOfParts_ShouldThrow() {
            Assert.Throws<ArgumentException>(() =>
                Program.Utils.GetStepParts("test;1;2;3", 2)
            );
        }

        [TestCaseSource(nameof(GetStepPartsCases))]
        public void GetStepParts_GivenValidSerializedStep_ShouldReturnValidParts(
            string givenSerializedStep,
            int givenRequiredCount,
            string[] expectedResult
        ) {
            var actualResult = Program.Utils.GetStepParts(givenSerializedStep, givenRequiredCount);

            Assert.AreEqual(expectedResult, actualResult);
        }

        public static object[] GetStepPartsCases = {
            new object[] {
                "TEST", 1,
                new string[] { "TEST" }
            },
            new object[] {
                "TEST;some;param", 3,
                new string[] { "TEST", "some", "param" }
            }
        };
    }
}