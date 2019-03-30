using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;

namespace CaravanAutoBedroll
{
    [StaticConstructorOnStartup]
    public class CaravanAutoBedroll
    {
        static CaravanAutoBedroll()
        {
            LogMessage($"Initializing");

            var harmony = HarmonyInstance.Create("RimWorld-CaravanAutoBedroll");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(RimWorld.Planet.CaravanFormingUtility),
                      "StartFormingCaravan",
                      new Type[] { typeof(List<Pawn>), typeof(List<Pawn>), typeof(Faction), typeof(List<TransferableOneWay>), typeof(IntVec3), typeof(IntVec3), typeof(int), typeof(int) })]
        class CaravanFormingUtility_StartFormingCaravan
        {
            static void Prefix(List<Pawn> pawns, List<Pawn> downedPawns, Faction faction, List<TransferableOneWay> transferables, IntVec3 meetingPoint, IntVec3 exitSpot, int startingTile, int destinationTile)
            {
                try
                {
                    LogMessage("Prefixing CaravanFormingUtility.StartFormingCaravan");

                    // Pre-calculations
                    var stuffList = transferables.Where(x => x.HasAnyThing && !(x.AnyThing is Pawn)).ToList();
                    var caravanList = stuffList.Where(x => x.CountToTransfer > 0).ToList();

                    var plannedBedrolls = caravanList.Where(IsBedroll).ToList();
                    var caravanColonists = pawns.Where(x => !x.AnimalOrWildMan()).ToList();
                    var bedrollsNeeded = caravanColonists.Count;

                    // Are there enough bedrolls already?
                    LogMessage($"Planning to bring {plannedBedrolls.Count} bedrolls");
                    LogMessage($"Need {bedrollsNeeded} bedrolls");

                    var bedrollDeficit = bedrollsNeeded - plannedBedrolls.Count;
                    if (bedrollDeficit <= 0)
                    {
                        // Satisfied, continue
                        return;
                    }

                    // Look for additional bedrolls
                    LogMessage($"Looking for {bedrollDeficit} additional bedrolls");
                    var availableBedrollList = stuffList.Where(IsBedroll).Where(x => x.CountToTransfer < x.MaxCount).ToList();
                    LogMessage($"Found {availableBedrollList.Count} unused minified bedroll piles");

                    if (!availableBedrollList.Any())
                    {
                        // Nothing found, nothing to do
                        return;
                    }

                    // TODO: calculate bedroll capacity and shared bed preference?

                    // TODO: check to make sure caravan has carrying capacity?

                    // Take best first 
                    var sortedBedrolls = availableBedrollList.OrderByDescending(GetBedrollSortValue);

                    // Add additional bedrolls until satisfied
                    var updatedBedrollDeficit = bedrollDeficit;
                    foreach (var availableBedroll in sortedBedrolls)
                    {
                        if (updatedBedrollDeficit <= 0)
                            break;

                        var numberAvailable = availableBedroll.MaxCount - availableBedroll.CountToTransfer;
                        var numberToAdd = numberAvailable > updatedBedrollDeficit
                            ? updatedBedrollDeficit
                            : numberAvailable;

                        availableBedroll.AdjustBy(numberToAdd);
                        updatedBedrollDeficit -= numberToAdd;
                    }

                    var added = bedrollDeficit - updatedBedrollDeficit;
                    var newTotal = plannedBedrolls.Count + added;
                    LogMessage($"Added {added} bedrolls, for a total of {newTotal} out of {bedrollsNeeded} needed");
                }
                catch (Exception ex)
                {
                    LogError("Exception during CaravanFormingUtility.StartFormingCaravan Prefix");
                    LogError(ex.ToString());
                }
            }
        }

        public static bool IsBedroll(TransferableOneWay x)
        {
            if (x.AnyThing == null)
                return false;

            var minifiedThing = x.AnyThing.GetInnerIfMinified();
            if (minifiedThing == null || minifiedThing.def == null || minifiedThing.def.building == null)
                return false;

            return minifiedThing.def.building.bed_caravansCanUse;
        }

        public static float GetBedrollSortValue(TransferableOneWay x)
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
                LogMessage("Could not calculate comfort for " + x.Label);
            }

            return comfort;
        }

        public static void LogMessage(string message)
        {
            Log.Message("[CaravanAutoBedroll]" + message);
        }

        public static void LogError(string message)
        {
            Log.Error("[CaravanAutoBedroll]" + message);
        }
    }
}
