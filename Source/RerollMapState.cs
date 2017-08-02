using System.Collections.Generic;
using Verse;

namespace MapReroll {
	public class RerollMapState : IExposable {
		public bool RerollGenerated;
		public bool MapCommitted;
		public string RerollSeed;
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

		public void ExposeData() {
			Scribe_Values.Look(ref RerollGenerated, "rerollGenerated", false);
			Scribe_Values.Look(ref RerollSeed, "rerollSeed", null);
			Scribe_Values.Look(ref ResourceBalance, "resourceBalance", 0f);
			Scribe_Values.Look(ref NumPreviewPagesPurchased, "pagesPurchased", 0);
			Scribe_Values.Look(ref MapCommitted, "committed", false);
			Scribe_Defs.Look(ref UsedMapGenerator, "usedMapGenerator");
			Scribe_Collections.Look(ref _scenarioGeneratedThingIds, "scenarioGeneratedThingIds", LookMode.Value);
			Scribe_Collections.Look(ref _playerAddedThingIds, "playerAddedThingIds", LookMode.Value);
		}
	}
}