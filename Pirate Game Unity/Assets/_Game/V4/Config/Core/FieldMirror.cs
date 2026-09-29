using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// Copies public instance fields with the same name between mirrored settings classes (enums by value, arrays
    /// element-wise). Used by the V3 migrator (V3 → config) and the runtime spawner profiles (config → the shared spawn
    /// data types), so the two directions cannot drift. Returns the destination fields that had no source, so tests can
    /// prove the mirrors are complete.
    /// </summary>
    public static class FieldMirror
    {
        public static List<string> CopyFields(object source, object destination)
        {
            var missing = new List<string>();
            var src = source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).ToDictionary(f => f.Name);
            foreach (var d in destination.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!src.TryGetValue(d.Name, out var s)) { missing.Add(d.Name); continue; }
                d.SetValue(destination, Convert(s.GetValue(source), d.FieldType));
            }
            return missing;
        }

        static object Convert(object value, Type target)
        {
            if (value == null) return null;
            if (target.IsEnum) return Enum.ToObject(target, System.Convert.ToInt32(value));
            if (target.IsArray && value is Array array)
            {
                var element = target.GetElementType();
                var result = Array.CreateInstance(element, array.Length);
                for (var i = 0; i < array.Length; i++) result.SetValue(Convert(array.GetValue(i), element), i);
                return result;
            }
            // Mirrored settings classes (e.g. an unlock entry on each side): a new destination object, fields copied by name.
            if (target.IsClass && target != typeof(string) && !target.IsInstanceOfType(value) && !typeof(UnityEngine.Object).IsAssignableFrom(target)
                && target.GetConstructor(Type.EmptyTypes) != null)
            {
                var copy = Activator.CreateInstance(target);
                CopyFields(value, copy);
                return copy;
            }
            return value;
        }
    }

    /// <summary>Timeline tag caps the enemy world reads (concurrency tags, not content ids).</summary>
    public static class TagCapIds
    {
        public const string RangedGunner = "ranged_gunner", AreaDenial = "area_denial", ChargeWindup = "charge_windup";
    }

    /// <summary>Timeline phase-id conventions the runtime reads (not content ids).</summary>
    public static class PhaseIds
    {
        /// <summary>Ambient band phases are "band_0" … "band_4"; their rate sets the band spawner's interval.</summary>
        public const string BandPrefix = "band_";
    }
}
