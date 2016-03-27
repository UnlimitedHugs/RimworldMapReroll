using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using System.Linq;

namespace MapReroll {
	public class ModInitializerComponent : MonoBehaviour {
		private static bool injectionPerformed;
		private static bool popped;

		public void FixedUpdate() {
			if(Input.GetKeyDown(KeyCode.Keypad4)) {
				/*var x = new Genstep_ScatterThings();
				x.thingDefs = new List<ThingDef>(new[] { ThingDefOf.SteamGeyser });
				x.minSpacing = 25;
				x.extraNoBuildEdgeDist = 4;
				x.countPer10kCellsRange = new FloatRange(0.7f, 1f);
				x.clearSpaceSize = 30;
				x.neededSurfaceType = TerrainAffordance.Heavy;
				x.validators = new List<ScattererValidator>(new ScattererValidator[]{new ScattererValidator_Buildable(){radius = 4, affordance = TerrainAffordance.Heavy}, new ScattererValidator_NoNonNaturalEdifices(){radius = 4}});
				*/
				
				// scrap all existing geysers
				Thing.allowDestroyNonDestroyable = true;
				Find.ListerThings.ThingsOfDef(ThingDefOf.SteamGeyser).ForEach(t => t.Destroy());
				Thing.allowDestroyNonDestroyable = false;
				
				// find and re-run the geyser genstep
				var mapGenDef = DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();
				if (mapGenDef != null) {
					var geyserGen = mapGenDef.genSteps.Find(g => {
						var gen = g as Genstep_ScatterThings;
						return gen != null && gen.thingDefs.Count == 1 && gen.thingDefs[0] == ThingDefOf.SteamGeyser;
					});
					if (geyserGen != null) {
						geyserGen.Generate();
					}
				}
			}
			if (popped) return;
			if(Input.GetKeyDown(KeyCode.Keypad5)) {
				popped = true;
				Action newEventAction = delegate {
					var pawns = Find.ListerPawns.AllPawns.ToList();
					foreach (var pawn in pawns) {
						if(pawn.Faction == Faction.OfColony && pawn.SpawnedInWorld) {
							pawn.DeSpawn();
						}
					}
					/*var elev = MapGenerator.FloatGridNamed("Elevation");

					foreach (var cell in Find.Map.AllCells) {
						if(Rand.Range(0, 10)<=1) {
							elev[cell] = 1;
						}
					}*/
					Find.World.info.seedString = Rand.Int.ToString();
					MapInitData.mapToLoad = null;
					Application.LoadLevel("Gameplay");
				};
				LongEventHandler.QueueLongEvent(newEventAction, "LoadingLongEvent".Translate());
				
			}
			if (injectionPerformed || Game.Mode != GameMode.MapPlaying || Find.WindowStack == null) return;
			MapRerollController.OnLevelLoaded();
			RerollGUIController.Initialize();
			injectionPerformed = true;

			//var sec = Find.MapDrawer.SectionAt(new IntVec3());
			//var layersProp = typeof(Section).GetField("layers", BindingFlags.NonPublic | BindingFlags.Instance);
			//var layers = (List<SectionLayer>)layersProp.GetValue(sec);
			

			/*var sec = Find.MapDrawer.SectionAt(new IntVec3());
			var layer = sec.GetLayer(typeof(TestSection));
			Log.Message(layer.ToString());*/
		}

		public void OnLevelWasLoaded() {
			injectionPerformed = false;
			popped = false;
		}

		public void OnGUI() {
			if(injectionPerformed) RerollGUIController.OnGUI();
		}
	}
}
