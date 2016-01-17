using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using System.Linq;

namespace MapReroll {
	public class ModInitializerComponent : MonoBehaviour {
		private static bool injectionPerformed;
		private static bool popped;

		public void FixedUpdate() {
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
			if(injectionPerformed || Game.Mode != GameMode.MapPlaying) return;
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
	}
}
