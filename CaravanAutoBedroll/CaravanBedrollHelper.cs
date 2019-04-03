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
            var neededBedrolls = Mod.GetNeededBedrolls(dialogInstance.transferables, pawns);
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
                        $"Adding {neededBedrolls.Count} bedrolls would put caravan over weight by {neededMass - availableMass}. Accept or Confirm to leave as-is, or make changes to the caravan packing list try again.",
                        MessageTypeDefOf.RejectInput, false);

                    return false;
                }
                else
                {
                    // If trying the same thing twice in a row, allow caravan to leave without bedrolls
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
            }
            State.Forget();
            return true;
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
