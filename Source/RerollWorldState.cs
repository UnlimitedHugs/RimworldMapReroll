using RimWorld.Planet;
using Verse;

namespace MapReroll {
	public class RerollWorldState : WorldComponent {
		public int StartingTile = -1;

		public bool StartingTileIsKnown {
			get { return StartingTile >= 0; }
		}

		public RerollWorldState(World world) : base(world) {
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref StartingTile, "startingTile", -1);
		}
	}
}