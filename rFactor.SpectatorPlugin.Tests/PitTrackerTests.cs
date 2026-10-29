using Xunit;

namespace User.RfSpectator.Tests
{
    public class PitTrackerTests
    {
        static RfSnapshot Snap(float et, params RfVehicle[] cars)
        {
            return new RfSnapshot { ElapsedTime = et, NumVehicles = cars.Length, ByPlace = cars };
        }

        static RfVehicle Car(string driver, bool inPits)
        {
            return new RfVehicle { Driver = driver, Car = "C", InPits = inPits };
        }

        [Fact]
        public void EntryObserved_PitTimeGrowsFromEntryEt()
        {
            var t = new PitTracker();
            t.Apply(Snap(100f, Car("A", false)));
            var s2 = Snap(110f, Car("A", true));
            t.Apply(s2);
            Assert.Equal(0f, s2.ByPlace[0].PitTime);
            var s3 = Snap(125.5f, Car("A", true));
            t.Apply(s3);
            Assert.Equal(15.5f, s3.ByPlace[0].PitTime, 3);
        }

        [Fact]
        public void Exit_ResetsPitTimeToZero()
        {
            var t = new PitTracker();
            t.Apply(Snap(100f, Car("A", false)));
            t.Apply(Snap(110f, Car("A", true)));
            t.Apply(Snap(120f, Car("A", true)));
            var s = Snap(130f, Car("A", false));
            t.Apply(s);
            Assert.Equal(0f, s.ByPlace[0].PitTime);
        }

        [Fact]
        public void SecondStop_StartsFromNewEntry()
        {
            var t = new PitTracker();
            t.Apply(Snap(100f, Car("A", false)));
            t.Apply(Snap(110f, Car("A", true)));
            t.Apply(Snap(130f, Car("A", false)));
            t.Apply(Snap(200f, Car("A", true)));
            var s = Snap(207f, Car("A", true));
            t.Apply(s);
            Assert.Equal(7f, s.ByPlace[0].PitTime, 3);
        }

        [Fact]
        public void FirstSightingInPits_StaysZero()
        {
            var t = new PitTracker();
            t.Apply(Snap(10f, Car("A", true)));
            var s = Snap(50f, Car("A", true));
            t.Apply(s);
            Assert.Equal(0f, s.ByPlace[0].PitTime);
        }

        [Fact]
        public void FirstSightingInPits_ThenExitAndRealStop_Counts()
        {
            var t = new PitTracker();
            t.Apply(Snap(10f, Car("A", true)));
            t.Apply(Snap(20f, Car("A", false)));
            t.Apply(Snap(60f, Car("A", true)));
            var s = Snap(65f, Car("A", true));
            t.Apply(s);
            Assert.Equal(5f, s.ByPlace[0].PitTime, 3);
        }

        [Fact]
        public void Reset_ClearsState_SoCarInPitsIsFirstSightingAgain()
        {
            var t = new PitTracker();
            t.Apply(Snap(100f, Car("A", false)));
            t.Apply(Snap(110f, Car("A", true)));
            t.Reset();
            var s = Snap(120f, Car("A", true));
            t.Apply(s);
            Assert.Equal(0f, s.ByPlace[0].PitTime);
        }

        [Fact]
        public void PausedClock_DoesNotInflateTime()
        {
            var t = new PitTracker();
            t.Apply(Snap(100f, Car("A", false)));
            t.Apply(Snap(110f, Car("A", true)));
            t.Apply(Snap(115f, Car("A", true)));
            var s = Snap(115f, Car("A", true));
            t.Apply(s);
            Assert.Equal(5f, s.ByPlace[0].PitTime, 3);
        }

        [Fact]
        public void Cars_AreTrackedIndependently()
        {
            var t = new PitTracker();
            t.Apply(Snap(100f, Car("A", false), Car("B", false)));
            t.Apply(Snap(110f, Car("A", true), Car("B", false)));
            var s = Snap(120f, Car("A", true), Car("B", true));
            t.Apply(s);
            Assert.Equal(10f, s.ByPlace[0].PitTime, 3);
            Assert.Equal(0f, s.ByPlace[1].PitTime);
        }
    }
}
