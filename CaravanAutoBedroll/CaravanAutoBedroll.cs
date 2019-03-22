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
            Log.Message("[CaravanAutoBedroll] Initializing");

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
                Log.Message("[CaravanAutoBedroll] Prefixing CaravanFormingUtility.StartFormingCaravan");

                // Pre-calculations
                var stuffList = transferables.Where(x => x.HasAnyThing && !(x.AnyThing is Pawn)).ToList();
                var caravanList = stuffList.Where(x => x.CountToTransfer > 0).ToList();

                // TODO: Remove debug trace
                Log.Message("[CaravanAutoBedroll] Caravan bringing: ");
                foreach (var x in caravanList)
                {
                    Log.Message(ThingDebugString(x));
                }

                var plannedBedrolls = caravanList.Where(IsBedroll).ToList();
                var caravanColonists = pawns.Where(x => !x.AnimalOrWildMan()).ToList();
                var bedrollsNeeded = caravanColonists.Count;

                // Are there enough bedrolls already?
                Log.Message($"[CaravanAutoBedroll] Planning to bring {plannedBedrolls.Count} bedrolls");
                Log.Message($"[CaravanAutoBedroll] Need {bedrollsNeeded} bedrolls");

                var bedrollDeficit = bedrollsNeeded - plannedBedrolls.Count;
                if (bedrollDeficit <= 0)
                {
                    // Satisfied, continue
                    return;
                }

                // Look for additional bedrolls
                Log.Message($"[CaravanAutoBedroll] Looking for {bedrollDeficit} additional bedrolls");
                var availableBedrollList = stuffList.Where(IsBedroll).Where(x => x.CountToTransfer < x.MaxCount).ToList();
                Log.Message($"[CaravanAutoBedroll] Found {availableBedrollList.Count} unused minified bedrolls");

                if (!availableBedrollList.Any())
                {
                    // Nothing found, nothing to do
                    return;
                }

                // TODO: calculate bedroll capacity and shared bed preference?

                // TODO: check to make sure caravan has carrying capacity?

                // Take best first 
                var sortedBedrolls = availableBedrollList.OrderByDescending(GetQualityForSort);

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
                Log.Message($"[CaravanAutoBedroll] Added {added} bedrolls, for a total of {newTotal} out of {bedrollsNeeded} needed");
            }

            static bool IsBedroll(TransferableOneWay x)
            {
                if (x.AnyThing == null)
                    return false;

                var minifiedThing = x.AnyThing.GetInnerIfMinified();
                if (minifiedThing == null)
                    return false;

                if (minifiedThing.def == null || minifiedThing.def.building == null)
                    return false;

                return minifiedThing.def.building.bed_caravansCanUse;
            }

            static byte GetQualityForSort(TransferableOneWay x)
            {
                QualityCategory qc;
                if (!x.AnyThing.TryGetQuality(out qc))
                    qc = QualityCategory.Normal;

                return (byte)qc;
            }

            // TODO: Remove debug code once stable
            static string ThingDebugString(TransferableOneWay x)
            {
                var output = new StringBuilder();

                const string delimiter = "*";

                output.Append(x.Label);
                output.Append(delimiter);
                output.Append(x.CountToTransfer);
                output.Append("/");
                output.Append(x.MaxCount);

                output.Append(delimiter);
                output.Append("ThingDef:");
                output.Append(x.ThingDef);
                if (x.ThingDef != null)
                {
                    output.Append(delimiter);
                    output.Append(x.ThingDef.GetType());

                    output.Append(delimiter);
                    output.Append(x.ThingDef.building);
                }

                output.Append(delimiter);
                output.Append("AnyThing:");
                output.Append(x.AnyThing);
                if (x.AnyThing != null)
                {
                    output.Append(delimiter);
                    output.Append(x.AnyThing.GetType());

                    output.Append(delimiter);
                    output.Append(x.AnyThing.Label);

                    var innerThing = x.AnyThing.GetInnerIfMinified();
                    if (innerThing != null)
                    {
                        output.Append(delimiter);
                        output.Append("InnerThing:");
                        output.Append(innerThing.GetType());

                        output.Append(delimiter);
                        output.Append(innerThing.Label);

                        output.Append(delimiter);
                        output.Append(innerThing.def);

                        if (innerThing.def != null)
                        {
                            output.Append(delimiter);
                            output.Append(innerThing.def.building);

                            if (innerThing.def.building != null)
                            {
                                var building = innerThing.def.building;

                                output.Append(delimiter);
                                output.Append(building.bed_caravansCanUse);

                                output.Append(delimiter);
                                output.Append(building.GetType());
                            }
                        }
                    }
                }

                return output.ToString();
            }
        }
    }
}
