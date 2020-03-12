using System;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace MapReroll.Patches {
	/// <summary>
	/// Ensures that things delivered by caravan are registered to be carried over during a reroll
	/// </summary>
	[HarmonyPatch(typeof(CaravanEnterMapUtility))]
	[HarmonyPatch("Enter")]
	[HarmonyPatch(new []{typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool)})]
	//public static void Enter(Caravan caravan, Map map, Func<Pawn, IntVec3> spawnCellGetter, CaravanDropInventoryMode dropInventoryMode = CaravanDropInventoryMode.DoNotDrop, bool draftColonists = false)
	public static class CaravanEnterMapUtility_Enter_Patch {
		[HarmonyPrefix]
		public static void RecordPlayerAddedMapThings(Caravan caravan, Map map) {
			RerollToolbox.RecordPlayerAddedMapThings(caravan.pawns.Owner, map);
		}
	}
}