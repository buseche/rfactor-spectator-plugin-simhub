using System;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;

/**
 * 
 * User.RfSpectator
 * 
 * Plugin which reads the shared memory from rFactor and set all the data to show in Simhub
 * 
 * 
 */
namespace User.RfSpectator
{
    // Entry point: owns the reader and registers every SimHub property. Properties are pure functions of the
    // latest snapshot, so SimHub can poll them from any thread without locking.
    [PluginDescription("Tabla de posiciones de rFactor 1 en modo espectador (lee $rFactorShared$)")]
    [PluginAuthor("Proyecto")]
    [PluginName("RfSpectator")]
    public partial class RfSpectatorPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public RfSpectatorSettings Settings;
        public PluginManager PluginManager { get; set; }

        public ImageSource PictureIcon => this.ToIcon(null);
        public string LeftMenuTitle { get { return "RfSpectator"; } }

        ShmReader _reader;

        public void Init(PluginManager pluginManager)
        {
            // SimHub rebuilds plugins when the game changes, so Init may run several times.
            Settings = this.ReadCommonSettings<RfSpectatorSettings>("GeneralSettings", () => new RfSpectatorSettings());

            int max = Math.Max(1, Math.Min(Settings.MaxPositions, RfLayout.MaxVehicles));
            _reader = new ShmReader(Settings.MappingName, Settings.PollMs, Settings.StaleSeconds);

            RegisterSession();
            for (int i = 1; i <= max; i++) RegisterPosition(i);
            RegisterCarousel();

            _reader.Start();
            SimHub.Logging.Current.Info("RfSpectator: lector iniciado sobre '" + _reader.MappingName + "', " + max + " posiciones");
        }

        /// <summary>
        /// Required by IDataPlugin but intentionally empty: all data comes from the reader thread through the
        /// property delegates, so there is nothing to do per game-data tick.
        /// </summary>
        public void DataUpdate(PluginManager pluginManager, ref GameData data) { }

        public void End(PluginManager pluginManager)
        {
            // Must be safe to run more than once.
            var r = _reader;
            _reader = null;
            if (r != null) r.Dispose();
            if (Settings != null) this.SaveCommonSettings("GeneralSettings", Settings);
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SettingsControl(Settings, () => this.SaveCommonSettings("GeneralSettings", Settings), () => _reader);
        }

        // ---------------------------------------------------------------------------------------------
        // Properties. The delegates ALWAYS read the latest published snapshot; they never touch the mapping.
        // ---------------------------------------------------------------------------------------------

        /// <summary>Latest published snapshot, or null before the first valid read or after Stop.</summary>
        RfSnapshot Snap { get { var r = _reader; return r == null ? null : r.Snapshot; } }

        /// <summary>Connection state plus session-wide values, including the session's fastest lap.</summary>
        void RegisterSession()
        {
            this.AttachDelegate("Connected", () => _reader != null && _reader.Connected);
            this.AttachDelegate("DataFresh", () => _reader != null && _reader.DataFresh);
            this.AttachDelegate("Status", () => _reader == null ? "" : _reader.Status);

            this.AttachDelegate("Session.Id", () => { var s = Snap; return s == null ? 0 : s.SessionId; });
            this.AttachDelegate("Session.Phase", () => { var s = Snap; return s == null ? 0 : s.Phase; });
            this.AttachDelegate("Session.PhaseText", () => { var s = Snap; return s == null ? "" : RfFormat.PhaseText(s.Phase); });
            this.AttachDelegate("Session.YellowState", () => { var s = Snap; return s == null ? 0 : s.Yellow; });
            this.AttachDelegate("Session.YellowText", () => { var s = Snap; return s == null ? "" : RfFormat.YellowText(s.Yellow); });
            this.AttachDelegate("Session.ElapsedTime", () => { var s = Snap; return s == null ? 0.0 : (double)s.ElapsedTime; });
            this.AttachDelegate("Session.EndTime", () => { var s = Snap; return (s == null || float.IsNaN(s.EndTime)) ? 0.0 : (double)s.EndTime; });
            this.AttachDelegate("Session.TimeLeft", () =>
            {
                var s = Snap;
                if (s == null || float.IsNaN(s.EndTime)) return 0.0;
                return Math.Max(0.0, (double)s.EndTime - s.ElapsedTime);
            });
            this.AttachDelegate("Session.MaxLaps", () => { var s = Snap; return s == null ? 0 : s.MaxLaps; });          // D: unconfirmed
            this.AttachDelegate("Session.LapsLeft", () =>
            {
                var s = Snap;
                if (s == null || s.MaxLaps <= 0) return 0;
                var lead = s.AtPlace(1);
                return lead == null ? 0 : Math.Max(0, s.MaxLaps - lead.Laps);
            });
            this.AttachDelegate("Session.NumVehicles", () => { var s = Snap; return s == null ? 0 : s.NumVehicles; });
            this.AttachDelegate("Session.PlayerName", () => { var s = Snap; return s == null ? "" : s.PlayerName; });
            this.AttachDelegate("Session.Track", () => { var s = Snap; return s == null ? "" : s.Track; });            // empty in spectator mode
            this.AttachDelegate("Session.TrackTemp", () => { var s = Snap; return (s == null || float.IsNaN(s.TrackTemp)) ? 0.0 : (double)s.TrackTemp; });     // D
            this.AttachDelegate("Session.AmbientTemp", () => { var s = Snap; return (s == null || float.IsNaN(s.AmbientTemp)) ? 0.0 : (double)s.AmbientTemp; }); // D

            // Fastest lap of the session, so dashboards can show it without scanning every PosNN.BestLap.
            this.AttachDelegate("Session.FastestLap", () => { var v = FastestCar(); return v == null ? 0.0 : Pos(v.BestLap); });
            this.AttachDelegate("Session.FastestLapText", () => { var v = FastestCar(); return v == null ? "" : RfFormat.LapTime(v.BestLap); });
            this.AttachDelegate("Session.FastestLapDriver", () => { var v = FastestCar(); return v == null ? "" : v.Driver; });
            this.AttachDelegate("Session.FastestLapSurname", () => { var v = FastestCar(); return v == null ? "" : NameParts.Surname(v.Driver); });
            this.AttachDelegate("Session.FastestLapPlace", () => { var v = FastestCar(); return v == null ? 0 : v.Place; });
            this.AttachDelegate("Session.FastestLapCarNumber", () => { var v = FastestCar(); return v == null ? "" : NameParts.CarNumber(v.Car); });
            this.AttachDelegate("Session.FastestLapClass", () => { var v = FastestCar(); return v == null ? "" : v.Class; });
            this.AttachDelegate("Session.FastestLapColor", () => ColorOf(FastestCar()));
        }

        /// <summary>Registers every PosNN.* property for one leaderboard place.</summary>
        void RegisterPosition(int position)
        {
            // IMPORTANT: copy to a local variable. With the loop variable, every delegate
            // would end up pointing at the last position.
            int p = position;
            string n = "Pos" + p.ToString("00") + ".";

            this.AttachDelegate(n + "Driver", () => { var v = Row(p); return v == null ? "" : v.Driver; });
            this.AttachDelegate(n + "Surname", () => { var v = Row(p); return v == null ? "" : NameParts.Surname(v.Driver); });
            this.AttachDelegate(n + "FirstName", () => { var v = Row(p); return v == null ? "" : NameParts.FirstName(v.Driver); });
            this.AttachDelegate(n + "Car", () => { var v = Row(p); return v == null ? "" : v.Car; });
            this.AttachDelegate(n + "Class", () => { var v = Row(p); return v == null ? "" : v.Class; });
            this.AttachDelegate(n + "Laps", () => { var v = Row(p); return v == null ? 0 : v.Laps; });
            this.AttachDelegate(n + "Sector", () => { var v = Row(p); return v == null ? 0 : v.Sector; });
            this.AttachDelegate(n + "CarNumber", () => { var v = Row(p); return v == null ? "" : NameParts.CarNumber(v.Car); });
            this.AttachDelegate(n + "Color", () => ColorOf(Row(p)));

            this.AttachDelegate(n + "BestLap", () => { var v = Row(p); return v == null ? 0.0 : Pos(v.BestLap); });
            this.AttachDelegate(n + "BestLapText", () => { var v = Row(p); return v == null ? "" : RfFormat.LapTime(v.BestLap); });
            this.AttachDelegate(n + "LastLap", () => { var v = Row(p); return v == null ? 0.0 : Pos(v.LastLap); });
            this.AttachDelegate(n + "LastLapText", () => { var v = Row(p); return v == null ? "" : RfFormat.LapTime(v.LastLap); });
            this.AttachDelegate(n + "CurS1", () => { var v = Row(p); return v == null ? 0.0 : Pos(v.CurS1); });
            this.AttachDelegate(n + "CurS2", () => { var v = Row(p); return v == null ? 0.0 : Pos(v.CurS2); });

            this.AttachDelegate(n + "GapLeader", () => { var v = Row(p); return v == null ? 0.0 : (double)v.GapLeader; });
            this.AttachDelegate(n + "GapLeaderText", () => { var v = Row(p); return v == null ? "" : RfFormat.Gap(v, v.GapLeader); });
            this.AttachDelegate(n + "GapAhead", () => { var v = Row(p); return v == null ? 0.0 : (double)v.GapAhead; });
            this.AttachDelegate(n + "GapAheadText", () => { var v = Row(p); return v == null ? "" : RfFormat.Gap(v, v.GapAhead); });
            this.AttachDelegate(n + "LapsBehindLeader", () => { var v = Row(p); return v == null ? 0 : v.LapsBehindLeader; });      // ?

            this.AttachDelegate(n + "InPits", () => { var v = Row(p); return v != null && v.InPits; });
            this.AttachDelegate(n + "PitTime", () => { var v = Row(p); return v == null ? 0.0 : (double)v.PitTime; });
            this.AttachDelegate(n + "PitTimeText", () => { var v = Row(p); return v == null ? "" : RfFormat.LapTime(v.PitTime); });
            this.AttachDelegate(n + "HasFastestLap", () => { var v = Row(p); return IsFastest(v); });
            this.AttachDelegate(n + "Pitstops", () => { var v = Row(p); return v == null ? 0 : v.Pitstops; });                      // ?
            this.AttachDelegate(n + "Penalties", () => { var v = Row(p); return v == null ? 0 : v.Penalties; });                    // ?
            this.AttachDelegate(n + "Status", () => { var v = Row(p); return v == null ? "" : RfFormat.Status(v.FinishStatus); });
            this.AttachDelegate(n + "IsPlayer", () => { var v = Row(p); return v != null && v.IsPlayer; });
            this.AttachDelegate(n + "SpeedKmh", () => { var v = Row(p); return v == null ? 0.0 : (double)v.SpeedKmh; });
            this.AttachDelegate(n + "LapDist", () => { var v = Row(p); return v == null ? 0.0 : (double)v.LapDist; });
            this.AttachDelegate(n + "PosX", () => { var v = Row(p); return v == null ? 0.0 : (double)v.PosX; });
            this.AttachDelegate(n + "PosZ", () => { var v = Row(p); return v == null ? 0.0 : (double)v.PosZ; });
        }

        /// <summary>Vehicle at a leaderboard place in the latest snapshot, or null.</summary>
        RfVehicle Row(int place) { var s = Snap; return s == null ? null : s.AtPlace(place); }

        /// <summary>Holder of the session's fastest lap, or null when nobody has a valid lap.</summary>
        RfVehicle FastestCar() { var s = Snap; return s == null ? null : s.FastestLapCar; }

        /// <summary>True for the fastest-lap holder; compares by place so it follows the car as positions change.</summary>
        bool IsFastest(RfVehicle v) { var f = FastestCar(); return v != null && f != null && v.Place == f.Place; }

        /// <summary>User color for a car ("#RRGGBB"), "" when none; per-car color beats class color.</summary>
        string ColorOf(RfVehicle v)
        {
            var st = Settings;
            if (v == null || st == null) return "";
            return ColorResolver.Resolve(st.ClassColors, st.CarColors, v.Car, v.Class);
        }

        /// <summary>Lap time as a double, with 0 (not -1) for "no data" so dashboards can treat 0 as empty.</summary>
        static double Pos(float t) { return t > 0 ? (double)t : 0.0; }
    }
}
