using HugsLib.Utils;
using Verse;

namespace MapReroll {
	public class RerollWorldState : UtilityWorldObject {
		public int StartingTile = -1;

		public bool StartingTileIsKnown {
			get { return StartingTile >= 0; }
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref StartingTile, "startingTile", -1);
		}
	}
}