using System;
using System.Collections.Generic;

namespace User.RfSpectator
{
    /// <summary>
    /// Derives the live pit-lane time of each car from the InPits flag, which is all the shared memory offers.
    /// Runs on the reader thread only, on a snapshot that is not yet published, so mutating it is safe.
    /// </summary>
    internal sealed class PitTracker
    {
        // Last InPits value per car. A missing key means "never seen", which is what lets us tell a real
        // false->true entry apart from a car that was already in the garage when we first saw it.
        readonly Dictionary<string, bool> _lastInPits = new Dictionary<string, bool>();

        // Session ElapsedTime at which each car was observed entering the pits.
        readonly Dictionary<string, float> _entryEt = new Dictionary<string, float>();

        /// <summary>
        /// Fills RfVehicle.PitTime for every car. Uses the game clock (ElapsedTime), not wall time,
        /// so a paused session does not inflate the stop duration.
        /// </summary>
        public void Apply(RfSnapshot s)
        {
            if (s == null) return;
            foreach (var v in s.ByPlace)
            {
                if (v == null) continue;
                string key = v.Key;

                bool wasInPits;
                bool seen = _lastInPits.TryGetValue(key, out wasInPits);
                _lastInPits[key] = v.InPits;

                if (!v.InPits)
                {
                    _entryEt.Remove(key);
                    v.PitTime = 0f;
                    continue;
                }

                // Only a false->true transition we actually observed starts the timer; a car first seen
                // already in the pits (garage at session start, plugin attached mid-stop) stays at 0.
                if (seen && !wasInPits) _entryEt[key] = s.ElapsedTime;

                float entry;
                v.PitTime = _entryEt.TryGetValue(key, out entry) ? Math.Max(0f, s.ElapsedTime - entry) : 0f;
            }
        }

        /// <summary>Forget everything; called on a new session so old entry times cannot leak into it.</summary>
        public void Reset()
        {
            _lastInPits.Clear();
            _entryEt.Clear();
        }
    }
}
