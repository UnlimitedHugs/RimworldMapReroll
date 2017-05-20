using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MapReroll {
	public static class Compat_CrashLanding {
		private const string CrashLandingHardArrivePartTypeName = "CrashLanding.ScenPart_PlayerPawnsArriveMethodCrash";
		private const string PlayerPawnsArriveMethodDefName = "PlayerPawnsArriveMethod";

		// Crash Landing compatibility fix
		// Hard scenario: heal up colonists for a repeat crash landing
		public static void TryReplaceHardCrashLandingPawnStart(Scenario scenario) {
			var hardArrivePartType = GenTypes.GetTypeInAnyAssembly(CrashLandingHardArrivePartTypeName);
			if (hardArrivePartType == null) {
				// crash landing is not loaded
				return;
			}
			var scenParts = (List<ScenPart>)ReflectionCache.Scenario_Parts.GetValue(scenario);
			var partIndex = scenParts.FindIndex(p => p != null && p.GetType() == hardArrivePartType);
			if (partIndex >= 0) {
				var arriveMethodDef = DefDatabase<ScenPartDef>.GetNamedSilentFail(PlayerPawnsArriveMethodDefName);
				if (arriveMethodDef != null) {
					scenParts.RemoveAt(partIndex);
					var standingPart = new ScenPart_PlayerPawnsArriveMethod { def = arriveMethodDef };
					ReflectionCache.PawnArriveMethod_Method.SetValue(standingPart, PlayerPawnsArriveMethod.Standing);
					scenParts.Insert(partIndex, standingPart);
				} else {
					MapRerollController.Instance.Logger.Warning("PlayerPawnsArriveMethod Def not found. Crash Landing compat is off");
				}
			}
		}
	}
}