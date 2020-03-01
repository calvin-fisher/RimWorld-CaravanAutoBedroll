using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace CaravanAutoBedroll
{
    /// <summary>
    /// Global mod definitions
    /// </summary>
    [StaticConstructorOnStartup]
    public class Mod
    {
        static Mod()
        {
            LogMessage($"Initializing");
            var harmony = new Harmony("AndHobbes.CaravanAutoBedroll");

            Dialog_FormCaravan.Patch(harmony);
        }

        public static void LogTrace(string message)
        {
#if DEBUG
            Log.Message("[CaravanAutoBedroll]" + message);
#endif
        }

        public static void LogMessage(string message)
        {
            Log.Message("[CaravanAutoBedroll]" + message);
        }

        public static void LogWarning(string message)
        {
            Log.Warning("[CaravanAutoBedroll]" + message);
        }

        public static void LogError(string message)
        {
            Log.Error("[CaravanAutoBedroll]" + message);
        }
    }
}
