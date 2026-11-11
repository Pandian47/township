using System;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>One prize step of a weekend event: reach the goal, claim coins / gems / an event-only decoration.</summary>
    [Serializable]
    public sealed class EventMilestone
    {
        [Min(1)] public int goal = 1;
        [Min(0)] public int coins;
        [Min(0)] public int gems;
        [Tooltip("Event-only decoration this prize unlocks in the Build shop (Decorations).")]
        public BuildingDefinition decoration;

        public string RewardText()
        {
            var parts = new List<string>();
            if (coins > 0) parts.Add($"{coins:N0} coins");
            if (gems > 0) parts.Add(gems == 1 ? "1 gem" : $"{gems} gems");
            if (decoration != null) parts.Add(decoration.DisplayName);
            return string.Join(" + ", parts);
        }
    }

    /// <summary>
    /// A weekend event ("Harvest Festival"): what earns points and the prizes. Events take turns, one per weekend,
    /// in the order of GameContent → Weekend events.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Weekend Event", fileName = "Event_")]
    public sealed class EventDefinition : ScriptableObject
    {
        [Tooltip("Permanent id (saved). lower_snake_case, never rename.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [Tooltip("What earns points: HarvestCrops (per crop), MakeProducts (per product) or DeliverOrders (per order).")]
        [SerializeField] private AchievementKind pointsFrom = AchievementKind.HarvestCrops;
        [SerializeField] private List<EventMilestone> milestones = new List<EventMilestone>();

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public string Description => description;
        public AchievementKind PointsFrom => pointsFrom;
        public IReadOnlyList<EventMilestone> Milestones => milestones;
        public IEnumerable<int> Goals => milestones.Select(m => m.goal);

        /// <summary>"Crops harvested", "Products made", "Orders delivered".</summary>
        public string PointsLabel => pointsFrom switch
        {
            AchievementKind.MakeProducts => "Products made",
            AchievementKind.DeliverOrders => "Orders delivered",
            _ => "Crops harvested",
        };

        public void EditorConfigure(string newId, string name, string text, AchievementKind kind, List<EventMilestone> steps)
        {
            id = newId;
            displayName = name;
            description = text;
            pointsFrom = kind;
            milestones = steps;
        }
    }
}
