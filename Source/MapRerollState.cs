using RimWorld;
using Verse;

namespace MapReroll {
	/// <summary>
	/// Stores state information required for map rerolls in a save-storeable format.
	/// </summary>
	public class MapRerollState : IExposable {
		/// <summary>
		/// % of resources remaining as currency for rerolls
		/// </summary>
		public float ResourcesPercentBalance;
		/// <summary>
		/// the world seed before any rerolls
		/// </summary>
		public string OriginalWorldSeed;
		/// <summary>
		/// the world seed of the last performed reroll
		/// </summary>
		public string LastRerollSeed;
			
		// MapInitData information
		public bool HasInitData;
		public int StartingTile;
		public int MapSize;
		public Season StartingSeason;
		public bool Permadeath;

		public void ExposeData() {
			Scribe_Values.Look(ref ResourcesPercentBalance, "resourcesPercentBalance", 0);
			Scribe_Values.Look(ref OriginalWorldSeed, "originalWorldSeed", null);
			Scribe_Values.Look(ref LastRerollSeed, "lastRerollSeed", null);
			Scribe_Values.Look(ref HasInitData, "hasInitData", false);
			Scribe_Values.Look(ref StartingTile, "startingTile", 0);
			Scribe_Values.Look(ref MapSize, "mapSize", MapRerollController.DefaultMapSize);
			Scribe_Values.Look(ref StartingSeason, "startingSeason", Season.Undefined);
			Scribe_Values.Look(ref Permadeath, "permadeath", false);
		}
		
		public override string ToString() {
			return string.Format("[MapRerollState ResourcesPercentBalance: {0}, OriginalWorldSeed: {1}, LastRerollSeed: {2}, HasInitData: {3}, StartingTile: {4}, MapSize: {5}, StartingSeason: {6}, Permadeath: {7}]", ResourcesPercentBalance, OriginalWorldSeed, LastRerollSeed, HasInitData, StartingTile, MapSize, StartingSeason, Permadeath);
		}
	}
}