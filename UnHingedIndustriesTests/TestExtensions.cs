using System;

namespace UnHingedIndustriesTests {
    public static class TestsExtensions {
        public static void SetPrivatePropertyValue<T>(this object obj, string propName, T val) {
            var property = obj.GetType().GetProperty(propName);
            if (property == null) {
                throw new ArgumentException("no property with name " + propName + " exists for " + nameof(obj));
            }

            property.SetValue(obj, val, null);
        }
    }
}