using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace CaravanAutoBedroll
{
    /// <summary>
    /// Defines the mod interaction points with the (re)form caravan dialog 
    /// </summary>
    public static class Dialog_FormCaravan
    {
        public static void Patch(HarmonyInstance harmony)
        {
            harmony.Patch(
                original: typeof(RimWorld.Dialog_FormCaravan).GetMethod(
                    "CheckForErrors", 
                    BindingFlags.NonPublic | BindingFlags.Instance),
                postfix: new HarmonyMethod(typeof(Dialog_FormCaravan), 
                    "CheckForErrors_Postfix"));
        }

        static void CheckForErrors_Postfix(RimWorld.Dialog_FormCaravan __instance, ref bool __result, 
            ref bool ___massUsageDirty, ref float ___lastMassFlashTime,
            List<Pawn> pawns)
        {
            // Don't intevene if only closing dialog to choose route
            if (__instance.choosingRoute)
                return;

            // Don't intervene if other errors are already preventing the dialog from closing
            if (!__result)
                return;

            // Errors are evaluated a second time before rendering confirmation, for some reason... ignore that call
            var stackFrame = new StackFrame(1);
            var callingMethod = stackFrame.GetMethod();
            if (callingMethod.Name == "DoBottomButtons")
                return;

            // Inject bedroll check after other error validation
            Mod.LogTrace("Postfixing Dialog_FormCaravan.CheckForErrors");
            __result = CaravanBedrollHelper.CheckBeforeClosing(__instance, pawns);
            ___massUsageDirty = true;
        }
    }
}
