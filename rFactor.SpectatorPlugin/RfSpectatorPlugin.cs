using System;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;

// CONFIRMAR contra la demo (User.PluginSdkDemo/DataPluginDemo.cs) antes de compilar:
//  - los "using" de arriba (la demo puede necesitar alguno mas, p. ej. el de la extension ToIcon),
//  - la linea PictureIcon (se copia tal cual de la demo),
//  - la firma de AttachDelegate (plan F1.2: abrir SimHub.Plugins.dll en ILSpy).
// Este archivo NO se ha compilado contra SimHub: no hay SimHub en el entorno donde se escribio.
namespace User.RfSpectator
{
    [PluginDescription("Tabla de posiciones de rFactor 1 en modo espectador (lee $rFactorShared$)")]
    [PluginAuthor("Proyecto")]
    [PluginName("RfSpectator")]
    public partial class RfSpectatorPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public RfSpectatorSettings Settings;
        public PluginManager PluginManager { get; set; }

        // CONFIRMAR: copiar de la demo (usa el recurso del icono del proyecto).
        public ImageSource PictureIcon => this.ToIcon(Properties.Resources.sdkmenuicon);
        public string LeftMenuTitle { get { return "RfSpectator"; } }

        ShmReader _reader;

        public void Init(PluginManager pluginManager)
        {
            // SimHub reconstruye los plugins al cambiar de juego: Init puede ejecutarse varias veces.
            Settings = this.ReadCommonSettings<RfSpectatorSettings>("GeneralSettings", () => new RfSpectatorSettings());

            int max = Math.Max(1, Math.Min(Settings.MaxPositions, RfLayout.MaxVehicles));
            _reader = new ShmReader(Settings.MappingName, Settings.PollMs, Settings.StaleSeconds);

            RegisterSession();
            for (int i = 1; i <= max; i++) RegisterPosition(i);
            RegisterCarousel();

            _reader.Start();
            SimHub.Logging.Current.Info("RfSpectator: lector iniciado sobre '" + _reader.MappingName + "', " + max + " posiciones");
        }

        // No se usa: los datos llegan por el hilo propio, no por el lector nativo de SimHub
        // (que es justo el que no entrega oponentes en espectador). No depender de data.GameRunning.
        public void DataUpdate(PluginManager pluginManager, ref GameData data) { }

        public void End(PluginManager pluginManager)
        {
            // Debe poder ejecutarse varias veces.
            var r = _reader;
            _reader = null;
            if (r != null) r.Dispose();
            if (Settings != null) this.SaveCommonSettings("GeneralSettings", Settings);
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SettingsControl(() => _reader);
        }

        // ---------------------------------------------------------------------------------------------
        // Propiedades. Los delegates leen SIEMPRE el ultimo snapshot publicado; nunca tocan el mapping.
        // ---------------------------------------------------------------------------------------------

        RfSnapshot Snap { get { var r = _reader; return r == null ? null : r.Snapshot; } }

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
            this.AttachDelegate("Session.MaxLaps", () => { var s = Snap; return s == null ? 0 : s.MaxLaps; });          // D: sin confirmar
            this.AttachDelegate("Session.LapsLeft", () =>
            {
                var s = Snap;
                if (s == null || s.MaxLaps <= 0) return 0;
                var lead = s.AtPlace(1);
                return lead == null ? 0 : Math.Max(0, s.MaxLaps - lead.Laps);
            });
            this.AttachDelegate("Session.NumVehicles", () => { var s = Snap; return s == null ? 0 : s.NumVehicles; });
            this.AttachDelegate("Session.PlayerName", () => { var s = Snap; return s == null ? "" : s.PlayerName; });
            this.AttachDelegate("Session.Track", () => { var s = Snap; return s == null ? "" : s.Track; });            // vacio en espectador
            this.AttachDelegate("Session.TrackTemp", () => { var s = Snap; return (s == null || float.IsNaN(s.TrackTemp)) ? 0.0 : (double)s.TrackTemp; });     // D
            this.AttachDelegate("Session.AmbientTemp", () => { var s = Snap; return (s == null || float.IsNaN(s.AmbientTemp)) ? 0.0 : (double)s.AmbientTemp; }); // D
        }

        void RegisterPosition(int position)
        {
            // IMPORTANTE: copiar a una variable local. Con la variable del bucle, todos los delegates
            // terminarian apuntando a la ultima posicion.
            int p = position;
            string n = "Pos" + p.ToString("00") + ".";

            this.AttachDelegate(n + "Driver", () => { var v = Row(p); return v == null ? "" : v.Driver; });
            this.AttachDelegate(n + "Surname", () => { var v = Row(p); return v == null ? "" : NameParts.Surname(v.Driver); });
            this.AttachDelegate(n + "FirstName", () => { var v = Row(p); return v == null ? "" : NameParts.FirstName(v.Driver); });
            this.AttachDelegate(n + "Car", () => { var v = Row(p); return v == null ? "" : v.Car; });
            this.AttachDelegate(n + "Class", () => { var v = Row(p); return v == null ? "" : v.Class; });
            this.AttachDelegate(n + "Laps", () => { var v = Row(p); return v == null ? 0 : v.Laps; });
            this.AttachDelegate(n + "Sector", () => { var v = Row(p); return v == null ? 0 : v.Sector; });

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
            this.AttachDelegate(n + "Pitstops", () => { var v = Row(p); return v == null ? 0 : v.Pitstops; });                      // ?
            this.AttachDelegate(n + "Penalties", () => { var v = Row(p); return v == null ? 0 : v.Penalties; });                    // ?
            this.AttachDelegate(n + "Status", () => { var v = Row(p); return v == null ? "" : RfFormat.Status(v.FinishStatus); });
            this.AttachDelegate(n + "IsPlayer", () => { var v = Row(p); return v != null && v.IsPlayer; });
            this.AttachDelegate(n + "SpeedKmh", () => { var v = Row(p); return v == null ? 0.0 : (double)v.SpeedKmh; });
            this.AttachDelegate(n + "LapDist", () => { var v = Row(p); return v == null ? 0.0 : (double)v.LapDist; });
            this.AttachDelegate(n + "PosX", () => { var v = Row(p); return v == null ? 0.0 : (double)v.PosX; });
            this.AttachDelegate(n + "PosZ", () => { var v = Row(p); return v == null ? 0.0 : (double)v.PosZ; });
        }

        RfVehicle Row(int place) { var s = Snap; return s == null ? null : s.AtPlace(place); }

        static double Pos(float t) { return t > 0 ? (double)t : 0.0; }
    }
}
