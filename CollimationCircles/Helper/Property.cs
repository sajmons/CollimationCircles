using System;
using System.Linq;
using System.Reflection;

namespace CollimationCircles.Helper
{
    internal static class Property
    {
        public static string GetPropValue(object source, string propertyName)
        {
            var property = source.GetType().GetRuntimeProperties().FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            return $"{property?.GetValue(source)}";
        }
    }
}
