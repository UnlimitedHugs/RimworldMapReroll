using System;
using System.Linq;
using System.Reflection;
using EdB.PrepareCarefully;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MapReroll {
	public class PrepareCarefullyCompat {
		public static readonly string ModName = "EdB Prepare Carefully";

		private MethodInfo initerTryLoadNewestWorld;
		private MethodInfo initerGenerateContentsIntoCurrentMap;

		public void Initialize() {
			var success = false;
			MethodInfo sourceMethod = null;
			MethodInfo destMethod = null;
			try {
				// reflect the methods we plan to use
				var initerType = typeof(EdB.PrepareCarefully.MapIniter_NewGame);
				sourceMethod = initerType.GetMethod("InitNewGeneratedMap", BindingFlags.Static | BindingFlags.Public);
				destMethod = typeof(PrepareCarefullyCompat).GetMethod("ReroutedInitNewGeneratedMap", BindingFlags.Instance | BindingFlags.NonPublic);
				
				initerTryLoadNewestWorld = initerType.GetMethod("TryLoadNewestWorld", BindingFlags.Static | BindingFlags.NonPublic);
				initerGenerateContentsIntoCurrentMap = initerType.GetMethod("GenerateContentsIntoCurrentMap", BindingFlags.Static | BindingFlags.Public);

				if (sourceMethod != null && destMethod != null) {
					// here be dragons
					success = MapRerollUtility.TryDetourFromTo(sourceMethod, destMethod);
				}
				if (initerTryLoadNewestWorld == null || initerGenerateContentsIntoCurrentMap == null) success = false;
			} catch (Exception) {
				success = false;
			}
			if(success) {
				Log.Message("[MapReroll] Successfull detoured "+sourceMethod+" into "+destMethod);
				MapRerollController.Instance.OnMapRerolled += OnMapRerolled;
			} else {
				Log.Warning("[MapReroll] Failed to detour EdB.PrepareCarefully.MapIniter_NewGame.InitNewGeneratedMap. Compatibility patch will not work.");
			}
		}

		private void OnMapRerolled() {
			// re-execute the custom gesteps
			var gensteps = DefDatabase<MapGeneratorDef>.AllDefs.First().genSteps;
			var colonistsGenstep = gensteps.First(g => g is EdB.PrepareCarefully.Genstep_Colonists);
			var scatterGenstep = gensteps.First(g => g is Genstep_SpawnStartingResources);
			if(colonistsGenstep==null||scatterGenstep==null) {
				Log.Error("[MapReroll] Failed to execute the Prepare Carfully specific gensteps");
				return;
			}
			colonistsGenstep.Generate();
			scatterGenstep.Generate();
		}

		/**
		 * Decompiled from the Prepare Carefully assembly.
		 * Only change is- the Reset() call at the end is removed, so that we can reuse the custom gensteps.
		 * Hopefully this will soon be replaced by an event hook, so that this crazy hack can be removed.
		 **/
		private void ReroutedInitNewGeneratedMap() {
			string str = GenText.ToCommaList(from mod in LoadedModManager.LoadedMods
											 select mod.name);
			Log.Message("Initializing new game with mods " + str);
			DeepProfiler.Start("InitNewGeneratedMap");
			if (!MapInitData.startedFromEntry) {
				Game.Mode = (GameMode)0;
				if (!((bool)initerTryLoadNewestWorld.Invoke(null, null))) {
					WorldGenerator.GenerateWorld();
				}
				MapInitData.ChooseDefaultStoryteller();
				MapInitData.ChooseDefaultDifficulty();
				Rand.RandomizeSeedFromTime();
				MapInitData.ChooseDecentLandingSite();
				MapInitData.GenerateDefaultColonistsWithFaction();
				MapInitData.colonyFaction.homeSquare = MapInitData.landingCoords;
				Find.FactionManager.Add(MapInitData.colonyFaction);
				FactionGenerator.EnsureRequiredEnemies(MapInitData.colonyFaction);
				MapInitData.colonyFaction = null;
				MapInitData.mapSize = 150;
			}
			DeepProfiler.Start("Set up map");
			Game.Mode = (GameMode)1;
			Find.RootMap.curMap = new Map();
			Find.Map.info.Size = new IntVec3(MapInitData.mapSize, 1, MapInitData.mapSize);
			Find.Map.info.worldCoords = MapInitData.landingCoords;
			Find.Map.storyteller = new Storyteller(MapInitData.chosenStoryteller, MapInitData.difficulty);
			MapIniterUtility.ReinitStaticMapComponents_PreConstruct();
			Find.Map.ConstructComponents();
			MapIniterUtility.ReinitStaticMapComponents_PostConstruct();
			if (MapInitData.startingMonth == (Month)12) {
				MapInitData.startingMonth = GenTemperature.EarliestMonthInTemperatureRange(16f, 9999f);
				if (MapInitData.startingMonth == (Month)12) {
					MapInitData.startingMonth = (Month)5;
				}
			}
			Find.TickManager.gameStartAbsTick = 300000 * (int)MapInitData.startingMonth + 7500;
			DeepProfiler.End();
			DeepProfiler.Start("Generate contents into map");

			// need to get the method here again, lest the target MethodInfo mysteriously becomes null
			// scratching this up to side effects from the detour black magic
			var genMethod = typeof(EdB.PrepareCarefully.MapIniter_NewGame).GetMethod("GenerateContentsIntoCurrentMap", BindingFlags.Static | BindingFlags.Public);
			genMethod.Invoke(null, new object[] { DefDatabase<MapGeneratorDef>.GetRandom() });
			
			DeepProfiler.End();
			Find.AreaManager.InitForNewGame();
			DeepProfiler.Start("Finalize map init");
			MapIniterUtility.FinalizeMapInit();
			DeepProfiler.End();
			DeepProfiler.End();
			Find.CameraMap.JumpTo(MapGenerator.PlayerStartSpot);
			if (MapInitData.startedFromEntry) {
				Find.MusicManagerMap.disabled = true;
				DiaNode diaNode = new DiaNode(Translator.Translate("GameStartDialog"));
				DiaOption diaOption = new DiaOption();
				diaOption.resolveTree = true;
				diaOption.action = delegate {
					Find.MusicManagerMap.ForceSilenceFor(7f);
					Find.MusicManagerMap.disabled = false;
				};
				diaOption.playClickSound = false;
				diaNode.options.Add(diaOption);
				Dialog_NodeTree dialog_NodeTree = new Dialog_NodeTree(diaNode);
				dialog_NodeTree.soundClose = SoundDef.Named("GameStartSting");
				Find.WindowStack.Add(dialog_NodeTree);
			} 
		}
	}
}
