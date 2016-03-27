using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MapReroll {
	public static class MapRerollController {
		public enum MapRerollType {
			Map, Geyser
		}

		// feel free to use these to detect reroll events 
		public static event Action OnMapRerolled;
		public static event Action OnGeysersRerolled;

		public static MapRerollDef SettingsDef { get; private set; }

		public static float ResourcePercentageRemaining { get; private set; }

		public static bool RerollTimeExpired {
			get { return Find.TickManager.TicksGame > SettingsDef.rerollTimeLimit; }
		}

		public static bool ShowInterface {
			get { return SettingsDef!=null && SettingsDef.enableInterface && !RerollTimeExpired; }
		}

		private static bool mapRerollTriggered;

		public static void OnLevelLoaded() {
			SettingsDef = DefDatabase<MapRerollDef>.GetNamed("mapRerollSettings");
			if(mapRerollTriggered) {
				ConsumeResourcePercentage(SettingsDef.mapRerollCost);
				mapRerollTriggered = false;
				OnMapRerolled();
			} else {
				ResourcePercentageRemaining = 100f;
			}
		}

		public static void RerollMap() {
			mapRerollTriggered = true;
			Action newEventAction = delegate {
				var pawns = Find.ListerPawns.AllPawns.ToList();
				foreach (var pawn in pawns) {
					// preserve colonists for next map load
					if (pawn.Faction == Faction.OfColony && pawn.SpawnedInWorld) {
						pawn.DeSpawn();
					}
				}
				Find.World.info.seedString = Rand.Int.ToString();
				MapInitData.mapToLoad = null;
				Application.LoadLevel("Gameplay");
			};
			LongEventHandler.QueueLongEvent(newEventAction, GetLoadingMessage());
		}
		
		public static void RerollGeysers() {
			var geyserGen = TryFindGeyserGenstep();
			if (geyserGen != null) {
				// trash existing geysers
				Thing.allowDestroyNonDestroyable = true;
				Find.ListerThings.ThingsOfDef(ThingDefOf.SteamGeyser).ForEach(t => t.Destroy());
				Thing.allowDestroyNonDestroyable = false;
				// poke some new ones
				geyserGen.Generate();
				ConsumeResourcePercentage(SettingsDef.geyserRerollCost);
				OnGeysersRerolled();
			} else {
				Log.Error("Failed to find the Genstep for geysers. Check your map generator config.");
			}
		}

		public static bool CanAffordOperation(MapRerollType type) {
			float cost = 0;
			switch (type) {
				case MapRerollType.Map: cost = SettingsDef.mapRerollCost; break;
				case MapRerollType.Geyser: cost = SettingsDef.geyserRerollCost; break;
			}
			return ResourcePercentageRemaining >= cost;
		}

		public static void ConsumeResourcePercentage(float percent) {
			var newResources = Mathf.Clamp(ResourcePercentageRemaining - percent, 0, 100);
			

			ResourcePercentageRemaining = newResources;
		}

		private static Genstep TryFindGeyserGenstep() {
			var mapGenDef = DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();
			if (mapGenDef == null) return null;
			return mapGenDef.genSteps.Find(g => {
				var gen = g as Genstep_ScatterThings;
				return gen != null && gen.thingDefs.Count == 1 && gen.thingDefs[0] == ThingDefOf.SteamGeyser;
			});
		}

		private static string GetLoadingMessage() {
			if(SettingsDef.useSillyLoadingMessages) {
				var messageIndex = UnityEngine.Random.Range(0, SettingsDef.numLoadingMessages - 1);
				var messageKey = "MapReroll_loading" + messageIndex;
				if (messageKey.CanTranslate()) {
					return messageKey.Translate();
				}
			}
			return "MapReroll_defaultLoadingMsg".Translate();
		}
	}
}