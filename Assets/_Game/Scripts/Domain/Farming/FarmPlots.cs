using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    public enum PlotStage
    {
        Empty,
        Growing,
        Ready,
    }

    /// <summary>A crop growing on one field. Times are absolute UTC milliseconds, so growth continues while the app is closed.</summary>
    public sealed class CropState
    {
        public CropState(string cropId, long plantedAtUnixMs, long growDurationMs)
        {
            CropId = cropId;
            PlantedAtUnixMs = plantedAtUnixMs;
            GrowDurationMs = Math.Max(0, growDurationMs);
        }

        public string CropId { get; }
        public long PlantedAtUnixMs { get; internal set; }
        public long GrowDurationMs { get; }
        public long ReadyAtUnixMs => PlantedAtUnixMs + GrowDurationMs;

        public bool IsReady(long nowUnixMs) => nowUnixMs >= ReadyAtUnixMs;

        /// <summary>0..1 growth progress. A device clock set backwards just shows 0 instead of breaking.</summary>
        public float Progress(long nowUnixMs)
        {
            if (GrowDurationMs <= 0) return 1f;
            double p = (double)(nowUnixMs - PlantedAtUnixMs) / GrowDurationMs;
            return (float)(p < 0 ? 0 : p > 1 ? 1 : p);
        }

        public long RemainingMs(long nowUnixMs) => Math.Max(0, ReadyAtUnixMs - nowUnixMs);
    }

    /// <summary>
    /// Crop state of every field plot, keyed by the field's building instance id.
    /// Pure rules: knows nothing about yields, inventory or visuals.
    /// </summary>
    public sealed class FarmPlots
    {
        private readonly Dictionary<string, CropState> _crops = new Dictionary<string, CropState>();

        public event Action<string> PlotChanged;

        public IReadOnlyDictionary<string, CropState> Crops => _crops;

        public PlotStage GetStage(string plotId, long nowUnixMs)
        {
            if (plotId == null || !_crops.TryGetValue(plotId, out var crop)) return PlotStage.Empty;
            return crop.IsReady(nowUnixMs) ? PlotStage.Ready : PlotStage.Growing;
        }

        public bool TryGetCrop(string plotId, out CropState crop)
        {
            crop = null;
            return plotId != null && _crops.TryGetValue(plotId, out crop);
        }

        public bool TryPlant(string plotId, string cropId, long growDurationMs, long nowUnixMs)
        {
            if (string.IsNullOrEmpty(plotId)) throw new ArgumentException("Plot id is required.", nameof(plotId));
            if (string.IsNullOrEmpty(cropId)) throw new ArgumentException("Crop id is required.", nameof(cropId));
            if (_crops.ContainsKey(plotId)) return false;

            _crops[plotId] = new CropState(cropId, nowUnixMs, growDurationMs);
            PlotChanged?.Invoke(plotId);
            return true;
        }

        /// <summary>Removes a ready crop and returns its id; fails (null) if the plot is empty or still growing.</summary>
        public string TryHarvest(string plotId, long nowUnixMs)
        {
            if (!TryGetCrop(plotId, out var crop) || !crop.IsReady(nowUnixMs)) return null;
            _crops.Remove(plotId);
            PlotChanged?.Invoke(plotId);
            return crop.CropId;
        }

        /// <summary>Makes a growing crop ready immediately (rewarded ad, premium currency).</summary>
        public bool FinishNow(string plotId, long nowUnixMs)
        {
            if (!TryGetCrop(plotId, out var crop) || crop.IsReady(nowUnixMs)) return false;
            crop.PlantedAtUnixMs = nowUnixMs - crop.GrowDurationMs;
            PlotChanged?.Invoke(plotId);
            return true;
        }

        /// <summary>Called when the field is removed; the crop is lost.</summary>
        public bool Clear(string plotId)
        {
            if (plotId == null || !_crops.Remove(plotId)) return false;
            PlotChanged?.Invoke(plotId);
            return true;
        }

        /// <summary>Loads a saved crop (no events, no validation of timing).</summary>
        public void Restore(string plotId, string cropId, long plantedAtUnixMs, long growDurationMs)
        {
            if (string.IsNullOrEmpty(plotId) || string.IsNullOrEmpty(cropId)) return;
            _crops[plotId] = new CropState(cropId, plantedAtUnixMs, growDurationMs);
        }
    }
}
