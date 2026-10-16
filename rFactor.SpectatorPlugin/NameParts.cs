using System;
using System.Collections.Generic;

namespace User.RfSpectator
{
    /// <summary>
    /// Separa "Nombre Apellido" en nombre y apellido. Regla: el apellido es la ultima palabra, mas las
    /// particulas que la preceden (van, von, de, del, ...). Un sufijo final (Jr, Sr, II, III) se ignora
    /// para decidir el apellido pero se conserva dentro de el. Un nombre de una sola palabra es todo apellido.
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

        /// <summary>Indice de la primera palabra del apellido.</summary>
        static int SurnameStart(string[] t)
        {
            int last = t.Length - 1;
            // un sufijo ("Jr.") se queda con el apellido que le precede
            if (last >= 1 && Suffixes.Contains(t[last])) last--;
            int start = last;
            // la primera palabra siempre es el nombre de pila
            while (start - 1 >= 1 && Particles.Contains(t[start - 1])) start--;
            return start;
        }

        static string[] Split(string full)
        {
            if (string.IsNullOrWhiteSpace(full)) return new string[0];
            return full.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string Surname(string full)
        {
            var t = Split(full);
            if (t.Length == 0) return "";
            if (t.Length == 1) return t[0];
            return string.Join(" ", t, SurnameStart(t), t.Length - SurnameStart(t));
        }

        public static string FirstName(string full)
        {
            var t = Split(full);
            if (t.Length <= 1) return "";
            return string.Join(" ", t, 0, SurnameStart(t));
        }
    }
}
