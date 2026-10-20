using NUnit.Framework;

namespace Hiking.Journey.Tests
{
    public class SeedHarvestTests
    {
        static JourneySession Session(int startingSeeds)
        {
            var session = new JourneySession();
            session.Begin(1, 1, new[] { new MaterialDefinition { id = "seed", startingCount = startingSeeds } });
            return session;
        }

        [Test]
        public void FirstEntryYieldsSeedsFromEachLivingStage()
        {
            var session = Session(0);
            var plants = new[]
            {
                new PlantInstance("plant", 0),
                new PlantInstance("plant", 1),
                new PlantInstance("plant", 2),
                new PlantInstance("plant", 2) { life = 0 }
            };

            Assert.That(session.CollectSeedsOnFirstEntry(3, plants), Is.EqualTo(2));
            Assert.That(session.Stock("seed"), Is.EqualTo(2));
            Assert.That(session.CollectSeedsOnFirstEntry(3, plants), Is.Zero);
            Assert.That(session.Stock("seed"), Is.EqualTo(2));
        }

        [Test]
        public void FirstStageYieldsNothingButStillUsesTheFirstEntry()
        {
            var session = Session(0);

            Assert.That(session.CollectSeedsOnFirstEntry(4,
                new[] { new PlantInstance("plant", 0) }), Is.Zero);
            Assert.That(session.CollectSeedsOnFirstEntry(4,
                new[] { new PlantInstance("plant", 2) }), Is.Zero);
            Assert.That(session.Stock("seed"), Is.Zero);
        }

        [Test]
        public void HarvestRespectsCapacityAndDoesNotRetryVisitedTile()
        {
            var session = Session(JourneySession.CarryCapacity - 1);
            var maturePlant = new[] { new PlantInstance("plant", 2) };

            Assert.That(session.CollectSeedsOnFirstEntry(1, maturePlant), Is.EqualTo(1));
            Assert.That(session.Stock("seed"), Is.EqualTo(JourneySession.CarryCapacity));
            Assert.That(session.CollectSeedsOnFirstEntry(2, maturePlant), Is.Zero);
            Assert.That(session.SeedCollectedTiles, Does.Contain(2));
        }

        [Test]
        public void BeginningAnotherLegResetsSeedsToConfiguredSupply()
        {
            var session = Session(10);
            Assert.That(session.CollectSeedsOnFirstEntry(1,
                new[] { new PlantInstance("plant", 2) }), Is.EqualTo(1));
            Assert.That(session.Stock("seed"), Is.EqualTo(11));

            session.Begin(1, 1, new[] { new MaterialDefinition { id = "seed", startingCount = 10 } });

            Assert.That(session.Stock("seed"), Is.EqualTo(10));
            Assert.That(session.SeedCollectedTiles, Is.Empty);
        }
    }
}
