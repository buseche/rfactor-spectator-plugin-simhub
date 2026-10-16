using System;
using SimHub.Plugins;

// Carrusel de posiciones (estilo transmision de TV): el bloque principal del dashboard muestra
// Pos01..Pos10 y el bloque inferior lee CarouselNN, que va rotando por las posiciones 11 en adelante.
// Requiere que la clase principal sea "partial" (ver instrucciones) y llamar RegisterCarousel() desde Init.
namespace User.RfSpectator
{
    public partial class RfSpectatorPlugin
    {
        // Ajustes del carrusel. Se cambian aqui y se recompila.
        const int CarouselStart = 11;          // primera posicion que entra al carrusel
        const int CarouselRows = 7;           // filas visibles del bloque inferior
        const double CarouselSeconds = 5.0;    // segundos por pagina

        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static double NowSeconds() { return (DateTime.UtcNow - Epoch).TotalSeconds; }

        int CarouselPages(RfSnapshot s) { return CarouselMath.Pages(s.NumVehicles, CarouselStart, CarouselRows); }

        int CarouselPage(RfSnapshot s) { return CarouselMath.Page(CarouselPages(s), NowSeconds(), CarouselSeconds); }

        /// <summary>Posicion real que muestra la fila r del carrusel en este momento (0 = vacia).</summary>
        int CarouselPlace(RfSnapshot s, int r)
        {
            return CarouselMath.PlaceAt(s.NumVehicles, CarouselStart, CarouselRows, CarouselPage(s), r);
        }

        RfVehicle CarouselRow(int r)
        {
            var s = Snap;
            if (s == null) return null;
            int place = CarouselPlace(s, r);
            return place == 0 ? null : s.AtPlace(place);
        }

        void RegisterCarousel()
        {
            this.AttachDelegate("Carousel.Pages", () => { var s = Snap; return s == null ? 0 : CarouselPages(s); });
            this.AttachDelegate("Carousel.Page", () => { var s = Snap; return (s == null || CarouselPages(s) == 0) ? 0 : CarouselPage(s) + 1; });
            this.AttachDelegate("Carousel.FirstPlace", () => { var s = Snap; return s == null ? 0 : CarouselPlace(s, 1); });
            this.AttachDelegate("Carousel.LastPlace", () =>
            {
                var s = Snap;
                if (s == null) return 0;
                for (int r = CarouselRows; r >= 1; r--) { int p = CarouselPlace(s, r); if (p != 0) return p; }
                return 0;
            });
            this.AttachDelegate("Carousel.RangeText", () =>
            {
                var s = Snap;
                if (s == null) return "";
                int first = CarouselPlace(s, 1);
                if (first == 0) return "";
                int last = first;
                for (int r = CarouselRows; r >= 1; r--) { int p = CarouselPlace(s, r); if (p != 0) { last = p; break; } }
                return first == last ? first.ToString() : first + "-" + last;
            });

            for (int i = 1; i <= CarouselRows; i++) RegisterCarouselRow(i);
        }

        void RegisterCarouselRow(int row)
        {
            int r = row;   // copiar a local: los delegates no deben compartir la variable del bucle
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
            this.AttachDelegate(n + "Status", () => { var v = CarouselRow(r); return v == null ? "" : RfFormat.Status(v.FinishStatus); });
            this.AttachDelegate(n + "IsPlayer", () => { var v = CarouselRow(r); return v != null && v.IsPlayer; });
        }
    }
}
