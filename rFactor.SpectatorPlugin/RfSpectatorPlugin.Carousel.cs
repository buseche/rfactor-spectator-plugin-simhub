using System;
using SimHub.Plugins;

// Position carousel (TV-broadcast style): the dashboard's main block shows Pos01..Pos10 and the lower
// block reads CarouselNN, which rotates through places 11 onwards so large grids fit on screen.
// Needs the main class to be "partial" and RegisterCarousel() to be called from Init.
namespace User.RfSpectator
{
    // Partial so carousel code lives apart from the core plugin; shares Snap and the Register* helpers.
    public partial class RfSpectatorPlugin
    {
        // Carousel tuning. Start and seconds are fixed (recompile); the row count comes from the settings panel.
        const int CarouselStart = 11;          // first place that enters the carousel
        const int CarouselMaxRows = RfSpectatorSettings.CarouselMaxRows;   // CarouselNN.* registered up to here
        const double CarouselSeconds = 5.0;    // seconds per page

        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static double NowSeconds() { return (DateTime.UtcNow - Epoch).TotalSeconds; }

        /// <summary>Live row count, re-read on every delegate call so the settings panel applies without restarting.</summary>
        int LiveRows { get { var st = Settings; return CarouselMath.ClampRows(st == null ? 7 : st.CarouselRows, CarouselMaxRows); } }

        int CarouselPages(RfSnapshot s) { return CarouselMath.Pages(s.NumVehicles, CarouselStart, LiveRows); }

        int CarouselPage(RfSnapshot s) { return CarouselMath.Page(CarouselPages(s), NowSeconds(), CarouselSeconds); }

        /// <summary>Real place shown by carousel row r right now (0 = empty).</summary>
        int CarouselPlace(RfSnapshot s, int r)
        {
            return CarouselMath.PlaceAt(s.NumVehicles, CarouselStart, LiveRows, CarouselPage(s), r);
        }

        /// <summary>Vehicle currently shown in carousel row r, or null.</summary>
        RfVehicle CarouselRow(int r)
        {
            var s = Snap;
            if (s == null) return null;
            int place = CarouselPlace(s, r);
            return place == 0 ? null : s.AtPlace(place);
        }

        /// <summary>Registers the paging info plus one property group per visible row.</summary>
        void RegisterCarousel()
        {
            this.AttachDelegate("Carousel.Pages", () => { var s = Snap; return s == null ? 0 : CarouselPages(s); });
            this.AttachDelegate("Carousel.Page", () => { var s = Snap; return (s == null || CarouselPages(s) == 0) ? 0 : CarouselPage(s) + 1; });
            this.AttachDelegate("Carousel.FirstPlace", () => { var s = Snap; return s == null ? 0 : CarouselPlace(s, 1); });
            this.AttachDelegate("Carousel.LastPlace", () =>
            {
                var s = Snap;
                if (s == null) return 0;
                for (int r = LiveRows; r >= 1; r--) { int p = CarouselPlace(s, r); if (p != 0) return p; }
                return 0;
            });
            this.AttachDelegate("Carousel.RangeText", () =>
            {
                var s = Snap;
                if (s == null) return "";
                int first = CarouselPlace(s, 1);
                if (first == 0) return "";
                int last = first;
                for (int r = LiveRows; r >= 1; r--) { int p = CarouselPlace(s, r); if (p != 0) { last = p; break; } }
                return first == last ? first.ToString() : first + "-" + last;
            });

            // All rows are registered up front; rows beyond LiveRows resolve to an empty place and return empty values.
            for (int i = 1; i <= CarouselMaxRows; i++) RegisterCarouselRow(i);
        }

        void RegisterCarouselRow(int row)
        {
            int r = row;   // copy to a local: the delegates must not share the loop variable
            string n = "Carousel" + r.ToString("00") + ".";

            this.AttachDelegate(n + "Place", () => { var v = CarouselRow(r); return v == null ? 0 : v.Place; });
            this.AttachDelegate(n + "Driver", () => { var v = CarouselRow(r); return v == null ? "" : v.Driver; });
            this.AttachDelegate(n + "Surname", () => { var v = CarouselRow(r); return v == null ? "" : NameParts.Surname(v.Driver); });
            this.AttachDelegate(n + "FirstName", () => { var v = CarouselRow(r); return v == null ? "" : NameParts.FirstName(v.Driver); });
            this.AttachDelegate(n + "Car", () => { var v = CarouselRow(r); return v == null ? "" : v.Car; });
            this.AttachDelegate(n + "Class", () => { var v = CarouselRow(r); return v == null ? "" : v.Class; });
            this.AttachDelegate(n + "Laps", () => { var v = CarouselRow(r); return v == null ? 0 : v.Laps; });
            this.AttachDelegate(n + "Sector", () => { var v = CarouselRow(r); return v == null ? 0 : v.Sector; });
            this.AttachDelegate(n + "BestLap", () => { var v = CarouselRow(r); return v == null ? 0.0 : Pos(v.BestLap); });
            this.AttachDelegate(n + "BestLapText", () => { var v = CarouselRow(r); return v == null ? "" : RfFormat.LapTime(v.BestLap); });
            this.AttachDelegate(n + "LastLap", () => { var v = CarouselRow(r); return v == null ? 0.0 : Pos(v.LastLap); });
            this.AttachDelegate(n + "LastLapText", () => { var v = CarouselRow(r); return v == null ? "" : RfFormat.LapTime(v.LastLap); });
            this.AttachDelegate(n + "GapLeader", () => { var v = CarouselRow(r); return v == null ? 0.0 : (double)v.GapLeader; });
            this.AttachDelegate(n + "GapLeaderText", () => { var v = CarouselRow(r); return v == null ? "" : RfFormat.Gap(v, v.GapLeader); });
            this.AttachDelegate(n + "GapAhead", () => { var v = CarouselRow(r); return v == null ? 0.0 : (double)v.GapAhead; });
            this.AttachDelegate(n + "GapAheadText", () => { var v = CarouselRow(r); return v == null ? "" : RfFormat.Gap(v, v.GapAhead); });
            this.AttachDelegate(n + "InPits", () => { var v = CarouselRow(r); return v != null && v.InPits; });
            this.AttachDelegate(n + "PitTime", () => { var v = CarouselRow(r); return v == null ? 0.0 : (double)v.PitTime; });
            this.AttachDelegate(n + "PitTimeText", () => { var v = CarouselRow(r); return v == null ? "" : RfFormat.LapTime(v.PitTime); });
            this.AttachDelegate(n + "HasFastestLap", () => { var v = CarouselRow(r); return IsFastest(v); });
            this.AttachDelegate(n + "Status", () => { var v = CarouselRow(r); return v == null ? "" : RfFormat.Status(v.FinishStatus); });
            this.AttachDelegate(n + "IsPlayer", () => { var v = CarouselRow(r); return v != null && v.IsPlayer; });
            this.AttachDelegate(n + "Color", () => ColorOf(CarouselRow(r)));
        }
    }
}
