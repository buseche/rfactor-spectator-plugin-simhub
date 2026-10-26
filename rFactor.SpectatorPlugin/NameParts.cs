using System;
using System.Collections.Generic;

namespace User.RfSpectator
{
    /// <summary>
    /// Splits "First Last" into first name and surname for dashboards that show them separately.
    /// Rule: the surname is the last word plus the particles before it (van, von, de, del, ...). A trailing
    /// suffix (Jr, Sr, II, III) is ignored when locating the surname but kept inside it. A single-word
    /// name is all surname.
    /// </summary>
    internal static class NameParts
    {
        static readonly HashSet<string> Particles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "van", "von", "der", "den", "de", "del", "della", "di", "da", "dos", "du", "la", "le", "ter", "ten"
        };

        static readonly HashSet<string> Suffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "jr", "jr.", "sr", "sr.", "ii", "iii", "iv"
        };

        /// <summary>Index of the first word of the surname.</summary>
        static int SurnameStart(string[] t)
        {
            int last = t.Length - 1;
            // a suffix ("Jr.") stays with the surname that precedes it
            if (last >= 1 && Suffixes.Contains(t[last])) last--;
            int start = last;
            // the first word is always the given name, so never walk past index 1
            while (start - 1 >= 1 && Particles.Contains(t[start - 1])) start--;
            return start;
        }

        static string[] Split(string full)
        {
            if (string.IsNullOrWhiteSpace(full)) return new string[0];
            return full.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>Surname, or the whole name when it is a single word.</summary>
        public static string Surname(string full)
        {
            var t = Split(full);
            if (t.Length == 0) return "";
            if (t.Length == 1) return t[0];
            return string.Join(" ", t, SurnameStart(t), t.Length - SurnameStart(t));
        }

        /// <summary>Everything before the surname; empty for single-word names.</summary>
        public static string FirstName(string full)
        {
            var t = Split(full);
            if (t.Length <= 1) return "";
            return string.Join(" ", t, 0, SurnameStart(t));
        }

        /// <summary>
        /// Car number taken from the car name when it starts with "#" (e.g. "#07 Mercedes C180" -> "07").
        /// The game does not provide the number as separate data: it depends on how the mod names the car.
        /// Kept as-is (leading zeros included). Without a "#", returns empty text.
        /// </summary>
        public static string CarNumber(string car)
        {
            if (string.IsNullOrEmpty(car)) return "";
            string t = car.TrimStart();
            if (t.Length < 2 || t[0] != '#') return "";
            int i = 1;
            while (i < t.Length && i <= 4 && char.IsLetterOrDigit(t[i])) i++;
            return t.Substring(1, i - 1);
        }
    }
}