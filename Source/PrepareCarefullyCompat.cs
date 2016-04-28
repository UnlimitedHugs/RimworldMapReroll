using EdB.PrepareCarefully;
using Verse;

namespace MapReroll {
	/**
	 * This is a temporary compatibility fix for the early release of Prepare Carefully for A13
	 */
	public class PrepareCarefullyCompat {
		public static readonly string ModName = "EdB Prepare Carefully";

		public void Notify_OnLevelLoaded() {
			// Prepare Carefully might already be fixed- in that case do nothing
			if (PrepareCarefully.Instance.Colonists.Count>0) return;
			if(!PrepareCarefully.Instance.Active) return;
			// Restore colonist entries. MapInitData.colonists were restored in MapRerollController
			foreach (var pawn in MapInitData.colonists) {
				PrepareCarefully.Instance.Colonists.Add(pawn);
			}
		}
	}
}