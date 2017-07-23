using Verse;

namespace MapReroll {
	public class Building_PirateStash : Building {
		public override void SpawnSetup(Map map, bool respawningAfterLoad) {
			Destroy();
		}
	}
}