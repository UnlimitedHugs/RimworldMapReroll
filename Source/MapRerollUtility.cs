using Verse;

namespace MapReroll {
	public static class MapRerollUtility {

		public static bool IsModActive(string modName) {
			foreach (var current in ModLister.AllInstalledMods) {
				if (modName.Equals(current.Name) && current.Active) {
					return true;
				}
			}
			return false;
		}
	}
}