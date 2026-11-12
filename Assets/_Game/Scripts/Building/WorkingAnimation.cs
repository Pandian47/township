using System;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// How a production building comes alive (v0.37.0), set per building on its <see cref="BuildingDefinition"/>.
    /// Points are 0-1 on the building's sprite, from its bottom-left (like the chimney point), so they follow the art.
    /// A zero point switches that part off. Played by <see cref="FactoryLife"/>.
    /// </summary>
    [Serializable]
    public sealed class WorkingAnimation
    {
        [Tooltip("Off = the building only smokes (older behaviour).")]
        public bool enabled;

        [Header("Idle")]
        [Tooltip("Where the sleepy Z letters rise from while nothing is being made.")]
        public Vector2 sleepPoint;

        [Header("Working")]
        [Tooltip("Where a stream of grains (or flour, sugar...) starts falling, e.g. the tip of a chute.")]
        public Vector2 streamPoint;
        [Tooltip("How far the stream falls, in world units (a grid cell is 1 wide, 0.5 tall).")]
        public float streamFall = 0.16f;
        public Color streamColor = new Color(1f, 0.78f, 0.2f);
        [Tooltip("Where little dust puffs rise (0 = none).")]
        public Vector2 dustPoint;
        public Color dustColor = new Color(0.98f, 0.93f, 0.8f, 0.7f);
        [Tooltip("A worker walks between these two points, carrying a sack one way (0 = no carrier).")]
        public Vector2 walkFrom;
        public Vector2 walkTo;
        [Tooltip("A second worker busy on the spot here (0 = none).")]
        public Vector2 workerSpot;
        [Tooltip("Gentle squash-and-stretch of the whole building while it works (0 = none).")]
        public float rumble;

        public static bool IsSet(Vector2 p) => p != Vector2.zero;
    }
}
