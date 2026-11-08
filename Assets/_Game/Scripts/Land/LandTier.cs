using System;
using System.Collections.Generic;
using UnityEngine;

namespace TownBuilder
{
    [Serializable]
    public struct MaterialCost
    {
        public MaterialDefinition material;
        [Min(1)] public int amount;
    }

    /// <summary>Price of one land plot in a ring around the start area (ring 1 = touching it).</summary>
    [Serializable]
    public sealed class LandTier
    {
        [Min(1)] public int unlockLevel = 1;
        [Min(0)] public int coins;
        public List<MaterialCost> materials = new List<MaterialCost>();
    }
}
