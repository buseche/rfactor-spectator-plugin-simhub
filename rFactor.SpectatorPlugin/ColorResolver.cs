using System;
using System.Collections.Generic;

namespace User.RfSpectator
{
    /// <summary>
    /// Resolves the display color of a car from user-defined class and per-car colors. Free of SimHub/WPF
    /// dependencies so it can be unit tested. Dictionaries are treated as immutable snapshots: edits build a
    /// new copy (With/Without) and the caller swaps the reference, so reader threads never see a half edit.
    /// </summary>
    internal static class ColorResolver
    {
        /// <summary>Returns "#RRGGBB" in upper case, or null when the text is not exactly that format.</summary>
        public static string Normalize(string hex)
        {
            if (hex == null) return null;
            string t = hex.Trim();
            if (t.Length != 7 || t[0] != '#') return null;
            for (int i = 1; i < 7; i++)
            {
                char c = t[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return null;
            }
            return t.ToUpperInvariant();
        }

        /// <summary>Per-car color wins over class color; empty string when nothing valid matches.</summary>
        public static string Resolve(IDictionary<string, string> classColors, IDictionary<string, string> carColors, string car, string cls)
        {
            string c = Lookup(carColors, car);
            if (c != null) return c;
            c = Lookup(classColors, cls);
            return c ?? "";
        }

        /// <summary>Copy of the map with key set to the normalized color; the same instance when key or color is invalid.</summary>
        public static Dictionary<string, string> With(Dictionary<string, string> map, string key, string hex)
        {
            string color = Normalize(hex);
            if (color == null || string.IsNullOrWhiteSpace(key)) return map;
            var copy = Copy(map);
            copy[key.Trim()] = color;
            return copy;
        }

        /// <summary>Copy of the map without key (case-insensitive).</summary>
        public static Dictionary<string, string> Without(Dictionary<string, string> map, string key)
        {
            var copy = Copy(map);
            if (key != null) copy.Remove(key.Trim());
            return copy;
        }

        static Dictionary<string, string> Copy(Dictionary<string, string> map)
        {
            // Always rebuilt with an ignore-case comparer: JSON deserialization may hand back a default-comparer map.
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (map != null)
                foreach (var kv in map) copy[kv.Key] = kv.Value;
            return copy;
        }

        static string Lookup(IDictionary<string, string> map, string key)
        {
            if (map == null || string.IsNullOrWhiteSpace(key)) return null;
            string trimmed = key.Trim();
            string value;
            if (map.TryGetValue(trimmed, out value)) return Normalize(value);
            // Fallback scan: a map deserialized from JSON may use the default (case-sensitive) comparer.
            foreach (var kv in map)
                if (string.Equals(kv.Key == null ? null : kv.Key.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    string n = Normalize(kv.Value);
                    if (n != null) return n;
                }
            return null;
        }
    }
}
