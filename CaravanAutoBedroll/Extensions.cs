using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CaravanAutoBedroll
{
    public static class Extensions
    {
        public static int GetSequenceHashCode<T>(this IEnumerable<T> sequence)
        {
            const int seed = 487;
            const int modifier = 31;

            unchecked
            {
                return sequence.Aggregate(seed, (current, item) =>
                    (current * modifier) + item.GetHashCode());
            }
        }

        public static bool IsBedroll(this TransferableOneWay x)
        {
            if (x.AnyThing == null)
                return false;

            var minifiedThing = x.AnyThing.GetInnerIfMinified();
            if (minifiedThing == null || minifiedThing.def == null || minifiedThing.def.building == null)
                return false;

            return minifiedThing.def.building.bed_caravansCanUse;
        }

        public static float GetBedrollSortValue(this TransferableOneWay x)
        {
            var comfort = 0f;
            var calculatedComfort = false;

            if (x.HasAnyThing)
            {
                var innerThing = x.AnyThing.GetInnerIfMinified();
                if (innerThing != null)
                {
                    comfort = innerThing.GetStatValue(StatDefOf.Comfort);
                    calculatedComfort = true;
                }
            }

            if (!calculatedComfort)
            {
                Mod.LogWarning("Could not calculate comfort for " + x.Label);
            }

            return comfort;
        }
    }
}
