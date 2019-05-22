using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CaravanAutoBedroll
{
    /// <summary>
    /// Logic for auto-adding bedrolls
    /// </summary>
    public static class CaravanBedrollHelper
    {
        /// <summary>
        /// Checks if more bedrolls are needed for the caravan, and adds them if appropriate
        /// </summary>
        /// <returns>
        /// Whether to proceed with sending the caravan
        /// </returns>
        public static bool CheckBeforeClosing(RimWorld.Dialog_FormCaravan dialogInstance, List<Pawn> pawns)
        {
            var neededBedrolls = GetNeededBedrolls(dialogInstance.transferables, pawns);
            if (neededBedrolls.Count <= 0)
                return true;

            var neededMass = GetNeededMass(neededBedrolls);
            Mod.LogTrace($"Need {neededMass} mass capacity for bedrolls");

            var availableMass = GetAvailableMass(dialogInstance);
            Mod.LogTrace($"Have {availableMass} mass capacity available");

            // Don't push the caravan over its mass limit
            if (neededMass > availableMass)
            {
                if (State.HasChanged(dialogInstance))
                {
                    // If the first time or if loadout changed, warn user and abort departure
                    Messages.Message(
                        $"Adding {neededBedrolls.Count} bedrolls would put caravan over weight by {neededMass - availableMass}. Accept or Confirm to leave as-is, or make changes to the caravan packing list to trigger re-processing.",
                        MessageTypeDefOf.RejectInput, false);

                    return false;
                }
                else
                {
                    // If trying the same thing twice in a row, allow caravan to leave without bedrolls
                    State.Forget();
                    return true;
                }
            }

            // Add needed bedrolls and leave
            if (neededBedrolls.Any())
            {
                foreach (var neededBedroll in neededBedrolls)
                {
                    neededBedroll.Key.AdjustBy(neededBedroll.Value);
                }
                Mod.LogMessage("Successfully added additional bedrolls");
                Messages.Message($"Successfully added {neededBedrolls.Count} bedrolls to caravan.", MessageTypeDefOf.RejectInput, false);
            }
            State.Forget();
            return true;
        }

        public static Dictionary<TransferableOneWay, int> GetNeededBedrolls(List<TransferableOneWay> transferables, List<Pawn> pawns = null)
        {
            Mod.LogMessage("Calculating needed bedrolls");
            var neededBedrolls = new Dictionary<TransferableOneWay, int>();

            // Pre-calculations
            pawns = pawns ?? TransferableUtility.GetPawnsFromTransferables(transferables);

            var colonyStuff = transferables
                .Where(x => x.HasAnyThing && !(x.AnyThing is Pawn))
                .ToList();
            var caravanIsBringing = colonyStuff
                .Where(x => x.CountToTransfer > 0)
                .ToList();

            var plannedBedrolls = caravanIsBringing
                .Where(x => x.IsBedroll())
                .ToList();
            var caravanColonists = pawns
                .Where(x => !x.AnimalOrWildMan())
                .ToList();
            var bedrollsNeeded = caravanColonists.Count;

            // Are there enough bedrolls already?
            Mod.LogMessage($"Planning to bring {plannedBedrolls.Count} bedrolls");
            Mod.LogMessage($"Need {bedrollsNeeded} bedrolls");

            var bedrollDeficit = bedrollsNeeded - plannedBedrolls.Count;
            if (bedrollDeficit <= 0)
            {
                // Satisfied, continue
                return neededBedrolls;
            }

            // Look for additional bedrolls
            Mod.LogMessage($"Looking for {bedrollDeficit} additional bedrolls");
            var availableBedrollList = colonyStuff
                .Where(x => x.IsBedroll())
                .Where(x => x.CountToTransfer < x.MaxCount)
                .ToList();
            Mod.LogMessage($"Found {availableBedrollList.Count} unused minified bedroll piles");

            if (!availableBedrollList.Any())
            {
                // Nothing found, nothing to do
                return neededBedrolls;
            }

            // Take best first 
            var sortedBedrolls = availableBedrollList.OrderByDescending(x => x.GetBedrollSortValue());

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

                neededBedrolls.Add(availableBedroll, numberToAdd);
                updatedBedrollDeficit -= numberToAdd;
            }

            var added = bedrollDeficit - updatedBedrollDeficit;
            var newTotal = plannedBedrolls.Count + added;
            Mod.LogMessage($"Planning to add {added} bedrolls, for a total of {newTotal} out of {bedrollsNeeded} needed");

            return neededBedrolls;
        }

        public static float GetNeededMass(Dictionary<TransferableOneWay, int> neededBedrolls) =>
            neededBedrolls.Sum(t => t.Key.AnyThing.GetStatValue(StatDefOf.Mass, true) * t.Value);

        public static float GetAvailableMass(RimWorld.Dialog_FormCaravan dialogInstance) =>
            dialogInstance.MassCapacity - dialogInstance.MassUsage;

        /// <summary>
        /// Tracks the state of the (re)form caravan dialog
        /// </summary>
        private static class State
        {
            private static RimWorld.Dialog_FormCaravan lastDialogInstance;
            private static int lastTransferablesHash;

            public static void Remember(RimWorld.Dialog_FormCaravan dialogInstance)
            {
                if (dialogInstance == null)
                {
                    Forget();
                    return;
                }

                var transferablesHash = CalculateTransferablesHash(dialogInstance.transferables);
                Remember(dialogInstance, transferablesHash);
            }

            private static void Remember(RimWorld.Dialog_FormCaravan dialogInstance, int transferablesHash)
            {
                lastDialogInstance = dialogInstance;
                lastTransferablesHash = transferablesHash;
            }

            public static void Forget()
            {
                lastDialogInstance = null;
                lastTransferablesHash = 0;
            }

            /// <summary>
            /// Calculates whether the dialog or any of its transferable items have changed since last remembered, 
            /// and records the new state if there were changes
            /// </summary>
            public static bool HasChanged(RimWorld.Dialog_FormCaravan dialogInstance)
            {
                if (dialogInstance == null)
                {
                    Mod.LogError("Dialog instance was null");
                    return true;
                }

                var hasChanged = false;
                if (lastDialogInstance != dialogInstance)
                {
                    Mod.LogTrace("Dialog instance changed");
                    hasChanged = true;
                }

                var transferablesHash = CalculateTransferablesHash(dialogInstance.transferables);
                if (transferablesHash != lastTransferablesHash)
                {
                    Mod.LogTrace("Transferables list changed");
                    hasChanged = true;
                }

                if (hasChanged)
                    Remember(dialogInstance, transferablesHash);

                return hasChanged;
            }

            /// <summary>
            /// Generates a hash value across the entire collection for determining uniqueness
            /// </summary>
            private static int CalculateTransferablesHash(List<TransferableOneWay> transferables)
            {
                var hashCollection = transferables.SelectMany(ExpandTransferableForHashing).ToList();
                var transferablesHash = hashCollection.GetSequenceHashCode();
                Mod.LogTrace("Calculated transferables hash " + transferablesHash);
                return transferablesHash;
            }

            private static IEnumerable<object> ExpandTransferableForHashing(TransferableOneWay transferable)
            {
                yield return transferable;
                yield return transferable.CountToTransfer;
            }
        }
    }
}
