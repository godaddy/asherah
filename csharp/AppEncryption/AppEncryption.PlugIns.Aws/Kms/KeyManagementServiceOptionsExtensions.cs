using System;
using System.Collections.Generic;
using System.Linq;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Extension methods for <see cref="KeyManagementServiceOptions"/>.
    /// </summary>
    public static class KeyManagementServiceOptionsExtensions
    {
        /// <summary>
        /// Returns a new instance of <see cref="KeyManagementServiceOptions"/> with the RegionKeyArns
        /// sorted by the priority regions order. Priority regions will appear first in the order specified,
        /// followed by any regions not in the priority list (maintaining their original order).
        /// </summary>
        /// <param name="options">The key management service options to optimize.</param>
        /// <param name="priorityRegions">Region names in the desired priority order.</param>
        /// <returns>A new <see cref="KeyManagementServiceOptions"/> instance with sorted RegionKeyArns.</returns>
        public static KeyManagementServiceOptions OptimizeByRegions(this KeyManagementServiceOptions options, params string[] priorityRegions)
        {
            // Create a dictionary for quick lookup of priority region indices
            var priorityRegionIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < priorityRegions.Length; i++)
            {
                if (!string.IsNullOrEmpty(priorityRegions[i]))
                {
                    priorityRegionIndices[priorityRegions[i]] = i;
                }
            }

            // Sort: priority regions first (by their index in priorityRegions), then non-priority (maintain original order)
            var sortedRegionKeyArns = options.RegionKeyArns
                .Select((rka, index) => new { RegionKeyArn = rka, OriginalIndex = index })
                .OrderBy(item =>
                {
                    if (priorityRegionIndices.TryGetValue(item.RegionKeyArn.Region, out var priorityIndex))
                    {
                        return priorityIndex;
                    }
                    // Non-priority regions go after all priority ones
                    return int.MaxValue;
                })
                .ThenBy(item => item.OriginalIndex) // Maintain original order for non-priority regions
                .Select(item => item.RegionKeyArn)
                .ToList();

            return new KeyManagementServiceOptions
            {
                RegionKeyArns = sortedRegionKeyArns
            };
        }
    }
}
