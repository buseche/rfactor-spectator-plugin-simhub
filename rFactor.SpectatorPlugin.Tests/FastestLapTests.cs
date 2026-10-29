using Xunit;

namespace User.RfSpectator.Tests
{
    public class FastestLapTests
    {
        static RfVehicle Car(int place, float best)
        {
            return new RfVehicle { Place = place, BestLap = best };
        }

        [Fact]
        public void LowestPositiveBestLapWins()
        {
            var cars = new[] { Car(1, 92.5f), Car(2, 91.2f), Car(3, 93f) };
            Assert.Equal(2, RfParser.FindFastestPlace(cars));
        }

        [Fact]
        public void IgnoresNonPositiveLaps()
        {
            var cars = new[] { Car(1, -1f), Car(2, 0f), Car(3, 95f) };
            Assert.Equal(3, RfParser.FindFastestPlace(cars));
        }

        [Fact]
        public void Tie_GoesToBetterPlacedCar()
        {
            var cars = new[] { Car(1, 92f), Car(2, 91f), Car(3, 91f) };
            Assert.Equal(2, RfParser.FindFastestPlace(cars));
        }

        [Fact]
        public void NoValidLaps_ReturnsZero()
        {
            var cars = new[] { Car(1, -1f), Car(2, -1f) };
            Assert.Equal(0, RfParser.FindFastestPlace(cars));
        }

        [Fact]
        public void EmptyGrid_ReturnsZero()
        {
            Assert.Equal(0, RfParser.FindFastestPlace(new RfVehicle[0]));
        }

        [Fact]
        public void FastestLapCar_ResolvesThroughPlace()
        {
            var cars = new[] { Car(1, 92f), Car(2, 90f) };
            var s = new RfSnapshot { ByPlace = cars, FastestLapPlace = 2 };
            Assert.Same(cars[1], s.FastestLapCar);
            Assert.Null(new RfSnapshot { ByPlace = cars }.FastestLapCar);
        }
    }
}
