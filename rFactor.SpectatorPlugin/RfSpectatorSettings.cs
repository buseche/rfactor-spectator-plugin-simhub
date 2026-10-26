using System;
using System.Collections.Generic;

namespace User.RfSpectator
{
    /// <summary>
    /// Settings persisted by SimHub (JSON). Mapping, positions and polling are read in Init (restart SimHub to
    /// apply); carousel rows and colors are read live and are edited from the plugin settings panel.
    /// </summary>
    public class RfSpectatorSettings
    {
        /// <summary>Upper bound of CarouselRows; CarouselNN.* is registered for every row up to this value at Init.</summary>
        public const int CarouselMaxRows = 20;

        /// <summary>Mapping name. Use a different one when testing with ShmFake (e.g. "$rFactorShared$Test").</summary>
        public string MappingName = "$rFactorShared$";

        /// <summary>Number of positions (PosNN) registered as properties. Maximum 128.</summary>
        public int MaxPositions = 40;

        /// <summary>Read interval in milliseconds.</summary>
        public int PollMs = 100;

        /// <summary>Seconds without a currentET change before DataFresh turns false.</summary>
        public double StaleSeconds = 5;

        /// <summary>Visible rows of the carousel block. Applied live; clamped to 1..CarouselMaxRows when read.</summary>
        public int CarouselRows = 7;

        // Class name / RfVehicle.Car -> "#RRGGBB". Edited on the UI thread, read on SimHub threads: never mutate
        // in place, replace the reference with a new copy (see ColorResolver.With/Without).
        public volatile Dictionary<string, string> ClassColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public volatile Dictionary<string, string> CarColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
