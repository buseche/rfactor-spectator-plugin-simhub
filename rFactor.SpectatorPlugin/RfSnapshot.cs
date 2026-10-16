using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace User.RfSpectator
{
    /// <summary>Un auto ya decodificado. Inmutable en la practica: solo lo crea el parser.</summary>
    public sealed class RfVehicle
    {
        public string Driver = "";
        public string Car = "";
        public string Class = "";
        public int Place;
        public int Laps;
        public int Sector;               // 1, 2 o 3
        public int FinishStatus;         // 0 corriendo, 1 termino, 2 DNF, 3 DQ
        public float LapDist;
        public float BestLap = -1;       // <= 0 = sin dato
        public float LastLap = -1;
        public float CurS1 = -1;
        public float CurS2 = -1;         // sector 2 aislado (acumulado - S1)
        public float GapLeader;          // segundos
        public float GapAhead;           // segundos
        public int LapsBehindLeader;
        public bool InPits;
        public bool IsPlayer;
        public int Pitstops;
        public int Penalties;
        public float SpeedKmh;
        public float PosX;
        public float PosZ;

        public string Key { get { return Driver + "|" + Car; } }
    }

    /// <summary>Foto completa de una lectura valida. Una vez publicada no se modifica.</summary>
    public sealed class RfSnapshot
    {
        public int SessionId;
        public float ElapsedTime;
        public float EndTime = float.NaN;    // NaN = sin limite de tiempo
        public int MaxLaps;
        public int Phase;
        public int Yellow;
        public bool InRealtime;
        public string PlayerName = "";
        public string Track = "";
        public float TrackTemp = float.NaN;
        public float AmbientTemp = float.NaN;
        public int NumVehicles;
        public RfVehicle[] ByPlace = new RfVehicle[0];   // indice 0 = P1
        public DateTime ReadAtUtc;

        public RfVehicle AtPlace(int place)
        {
            return (place >= 1 && place <= ByPlace.Length) ? ByPlace[place - 1] : null;
        }
    }

    internal static class RfParser
    {
        static readonly Encoding Cp1252 = Encoding.GetEncoding(1252);

        /// <summary>Lectura barata de sesion y tiempo para detectar sesion nueva antes del parseo completo.</summary>
        public static void PeekSession(byte[] b, out int session, out float et)
        {
            session = BitConverter.ToInt32(b, RfLayout.OffSession);
            et = BitConverter.ToSingle(b, RfLayout.OffCurrentET);
        }

        /// <summary>
        /// Valida y convierte el bloque. Devuelve false (con el motivo) si no tiene sentido:
        /// el escritor no tiene seqlock, asi que esta validacion es la unica defensa.
        /// latch: brechas congeladas de autos que ya terminaron (clave piloto|coche).
        /// </summary>
        public static bool TryParse(byte[] b, Dictionary<string, float[]> latch, out RfSnapshot snap, out string error)
        {
            snap = null; error = null;
            if (b == null || b.Length < RfLayout.MinSize) { error = "bloque demasiado corto"; return false; }

            int n = BitConverter.ToInt32(b, RfLayout.OffNumVehicles);
            if (n < 0 || n > RfLayout.MaxVehicles) { error = "numVehicles fuera de rango: " + n; return false; }

            float et = BitConverter.ToSingle(b, RfLayout.OffCurrentET);
            if (!IsFinite(et) || et < 0) { error = "currentET invalido"; return false; }

            var s = new RfSnapshot();
            s.SessionId = BitConverter.ToInt32(b, RfLayout.OffSession);
            s.ElapsedTime = et;
            float endEt = BitConverter.ToSingle(b, RfLayout.OffEndET);
            s.EndTime = (IsFinite(endEt) && endEt > 0 && endEt < 1e9f) ? endEt : float.NaN;  // centinela -> NaN
            int maxLaps = BitConverter.ToInt32(b, RfLayout.OffMaxLaps);
            s.MaxLaps = (maxLaps > 0 && maxLaps < 10000) ? maxLaps : 0;                       // D: sin confirmar
            s.Phase = b[RfLayout.OffGamePhase];
            s.Yellow = (sbyte)b[RfLayout.OffYellow];
            s.InRealtime = b[RfLayout.OffInRealtime] != 0;
            s.PlayerName = ReadString(b, RfLayout.OffPlayerName, RfLayout.LenPlayerName);
            s.Track = ReadString(b, RfLayout.OffTrackName, RfLayout.LenTrackName);
            s.TrackTemp = Sane(BitConverter.ToSingle(b, RfLayout.OffTrackTemp));
            s.AmbientTemp = Sane(BitConverter.ToSingle(b, RfLayout.OffAmbientTemp));
            s.NumVehicles = n;
            s.ReadAtUtc = DateTime.UtcNow;

            var byPlace = new RfVehicle[n];
            for (int i = 0; i < n; i++)
            {
                int o = RfLayout.VehicleBase + i * RfLayout.VehicleStride;
                int place = b[o + RfLayout.VPlace];
                if (place < 1 || place > n) { error = "place fuera de rango: " + place; return false; }
                if (byPlace[place - 1] != null) { error = "place repetido: " + place; return false; }

                var v = new RfVehicle();
                v.Place = place;
                v.Driver = ReadString(b, o + RfLayout.VDriver, RfLayout.VDriverLen);
                v.Car = ReadString(b, o + RfLayout.VCar, RfLayout.VCarLen);
                v.Class = ReadString(b, o + RfLayout.VClass, RfLayout.VClassLen);
                v.Laps = BitConverter.ToInt16(b, o + RfLayout.VTotalLaps);
                v.Sector = SectorToHuman((sbyte)b[o + RfLayout.VSector]);
                v.FinishStatus = (sbyte)b[o + RfLayout.VFinishStatus];
                v.InPits = b[o + RfLayout.VInPits] != 0;
                v.IsPlayer = b[o + RfLayout.VIsPlayer] != 0;
                v.Pitstops = BitConverter.ToInt16(b, o + RfLayout.VNumPitstops);
                v.Penalties = BitConverter.ToInt16(b, o + RfLayout.VNumPenalties);
                v.LapsBehindLeader = BitConverter.ToInt32(b, o + RfLayout.VLapsBehindLeader);

                float lapDist, best, last, c1, c2c, gl, ga, spd, px, pz;
                if (!Read(b, o + RfLayout.VLapDist, out lapDist) ||
                    !Read(b, o + RfLayout.VBestLap, out best) ||
                    !Read(b, o + RfLayout.VLastLap, out last) ||
                    !Read(b, o + RfLayout.VCurS1, out c1) ||
                    !Read(b, o + RfLayout.VCurS2, out c2c) ||
                    !Read(b, o + RfLayout.VGapLeader, out gl) ||
                    !Read(b, o + RfLayout.VGapNext, out ga) ||
                    !Read(b, o + RfLayout.VSpeed, out spd) ||
                    !Read(b, o + RfLayout.VPosX, out px) ||
                    !Read(b, o + RfLayout.VPosZ, out pz))
                { error = "float no finito en el auto " + i; return false; }

                v.LapDist = lapDist;
                v.BestLap = best > 0 ? best : -1;
                v.LastLap = last > 0 ? last : -1;
                v.CurS1 = c1 > 0 ? c1 : -1;
                v.CurS2 = (c1 > 0 && c2c > c1) ? c2c - c1 : -1;   // el campo es acumulado S1+S2
                v.GapLeader = gl;
                v.GapAhead = ga;
                v.SpeedKmh = spd * 3.6f;
                v.PosX = px;
                v.PosZ = pz;

                // Tras la meta lapDist y las brechas pasan a negativo: se congela la brecha del primer
                // snapshot con finishStatus != 0 (recortada a >= 0).
                if (v.FinishStatus != 0 && latch != null)
                {
                    float[] frozen;
                    if (!latch.TryGetValue(v.Key, out frozen))
                    {
                        frozen = new float[] { Math.Max(0f, gl), Math.Max(0f, ga) };
                        latch[v.Key] = frozen;
                    }
                    v.GapLeader = frozen[0];
                    v.GapAhead = frozen[1];
                }
                byPlace[place - 1] = v;
            }
            s.ByPlace = byPlace;
            snap = s;
            return true;
        }

        static bool Read(byte[] b, int off, out float value)
        {
            value = BitConverter.ToSingle(b, off);
            return IsFinite(value);
        }

        static bool IsFinite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        static float Sane(float f) { return (IsFinite(f) && f > -100f && f < 200f) ? f : float.NaN; }

        // 0 = sector 3, 1 = sector 1, 2 = sector 2
        static int SectorToHuman(int raw) { return raw == 0 ? 3 : (raw == 1 || raw == 2 ? raw : 0); }

        public static string ReadString(byte[] b, int off, int len)
        {
            int end = 0;
            while (end < len && b[off + end] != 0) end++;
            var sb = new StringBuilder(Cp1252.GetString(b, off, end));
            for (int i = 0; i < sb.Length; i++) if (char.IsControl(sb[i])) sb[i] = ' ';
            return sb.ToString().Trim();
        }
    }

    internal static class RfFormat
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>mm:ss.fff, o texto vacio si no hay dato.</summary>
        public static string LapTime(float t)
        {
            if (!(t > 0)) return "";
            int m = (int)(t / 60f);
            double s = t - m * 60.0;
            return m.ToString(Inv) + ":" + s.ToString("00.000", Inv);
        }

        public static string Gap(RfVehicle v, float seconds)
        {
            if (v.Place <= 1) return "";
            if (v.LapsBehindLeader > 0) return "+" + v.LapsBehindLeader.ToString(Inv) + "L";
            return "+" + seconds.ToString("0.000", Inv);
        }

        public static string PhaseText(int p)
        {
            switch (p)
            {
                case 0: return "Antes de empezar";
                case 1: return "Vueltas de reconocimiento";
                case 2: return "Parrilla";
                case 3: return "Vuelta de formacion";
                case 4: return "Cuenta atras";
                case 5: return "Verde";
                case 6: return "FCY";
                case 7: return "Sesion detenida";
                case 8: return "Sesion terminada";
                default: return "Desconocida";
            }
        }

        public static string YellowText(int y)
        {
            switch (y)
            {
                case 0: return "Ninguna";
                case 1: return "Pendiente";
                case 2: return "Boxes cerrados";
                case 3: return "Boxes: vuelta lider";
                case 4: return "Boxes abiertos";
                case 5: return "Ultima vuelta";
                case 6: return "Reanudar";
                case 7: return "Carrera detenida";
                default: return "";
            }
        }

        public static string Status(int fin)
        {
            switch (fin)
            {
                case 0: return "Corriendo";
                case 1: return "Terminado";
                case 2: return "DNF";
                case 3: return "DQ";
                default: return "";
            }
        }
    }
}
