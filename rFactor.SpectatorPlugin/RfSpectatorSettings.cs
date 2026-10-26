namespace User.RfSpectator
{
    /// <summary>Settings persisted by SimHub (JSON). Read in Init, so changing them requires restarting SimHub.</summary>
    public class RfSpectatorSettings
    {
        /// <summary>Mapping name. Use a different one when testing with ShmFake (e.g. "$rFactorShared$Test").</summary>
        public string MappingName = "$rFactorShared$";

        /// <summary>Number of positions (PosNN) registered as properties. Maximum 128.</summary>
        public int MaxPositions = 40;

        /// <summary>Read interval in milliseconds.</summary>
        public int PollMs = 100;

        /// <summary>Seconds without a currentET change before DataFresh turns false.</summary>
        public double StaleSeconds = 5;
    }
}
