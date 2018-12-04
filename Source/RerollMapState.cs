using System.Collections.Generic;
using System.IO;
using Verse;

namespace MapReroll {
	public class RerollMapState : IExposable {
		private readonly Map map;

		// saved
		public bool RerollGenerated;
		public bool MapCommitted;
		private string legacyMapSeed;
		public float ResourceBalance;
		public int NumPreviewPagesPurchased;
		public MapGeneratorDef UsedMapGenerator;
		private List<int> _scenarioGeneratedThingIds;
		private List<int> _playerAddedThingIds;

		// not included: colonists and their worn apparel
		public List<int> ScenarioGeneratedThingIds {
			get { return _scenarioGeneratedThingIds ?? (_scenarioGeneratedThingIds = new List<int>()); }
			set { _scenarioGeneratedThingIds = value; }
		}

		// thing ids imported by caravans and drop pods
		public List<int> PlayerAddedThingIds {
			get { return _playerAddedThingIds ?? (_playerAddedThingIds = new List<int>()); }
			set { _playerAddedThingIds = value; }
		}

		private MapSeed _mapSeed;
		public MapSeed MapSeed {
			get {
				if (_mapSeed == null) {
					if(map.Parent == null) throw new IOException("Could derive map seed- map is not assigned to a parent tile");
					_mapSeed = new MapSeed(Find.World.info.seedString, map.Tile, map.Size.x);
				}
				return _mapSeed;
			}
			set { _mapSeed = value; }
		}

		public RerollMapState(Map map) {
			this.map = map;
		}

		public void ExposeData() {
			Scribe_Values.Look(ref RerollGenerated, "rerollGenerated");
			Scribe_Values.Look(ref legacyMapSeed, "rerollSeed");
			LookMapSeed();
			Scribe_Values.Look(ref ResourceBalance, "resourceBalance");
			Scribe_Values.Look(ref NumPreviewPagesPurchased, "pagesPurchased");
			Scribe_Values.Look(ref MapCommitted, "committed");
			Scribe_Defs.Look(ref UsedMapGenerator, "usedMapGenerator");
			Scribe_Collections.Look(ref _scenarioGeneratedThingIds, "scenarioGeneratedThingIds", LookMode.Value);
			Scribe_Collections.Look(ref _playerAddedThingIds, "playerAddedThingIds", LookMode.Value);
		}

		private void LookMapSeed() {
			var seedString = _mapSeed?.ToString();
			Scribe_Values.Look(ref seedString, "mapSeed");
			if (Scribe.mode == LoadSaveMode.LoadingVars) {
				_mapSeed = MapSeed.TryParseLogError(seedString);
			}
		}

		internal void ConvertLegacyMapSeed() {
			if (legacyMapSeed != null) {
				if (_mapSeed == null) {
					_mapSeed = new MapSeed(legacyMapSeed, map.Tile, map.Size.x);
				}
				legacyMapSeed = null;
			}
		}
	}
}