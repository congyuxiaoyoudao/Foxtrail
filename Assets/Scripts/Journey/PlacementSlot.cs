using UnityEngine;

namespace Hiking.Journey
{
    public class PlacementSlot : MonoBehaviour
    {
        public string slotId;
        public Station station;
        public int tileIndex;
        public int SeedCount { get; private set; }
        public void ShowPlacement(MaterialDefinition material, int quantity, int currentStep)
        {
            if (material.id == "water") station.AddWater(tileIndex, quantity);
            else if (material.id == "seed")
            {
                station.AddSeeds(tileIndex, quantity, currentStep);
                SeedCount += quantity;
            }
        }
    }
}
