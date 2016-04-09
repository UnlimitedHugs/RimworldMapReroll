using Verse;

namespace MapReroll {
	public static class MapRerollUtility {

		public static bool IsModActive(string modName) {
			foreach (InstalledMod current in InstalledModLister.AllInstalledMods) {
				if (modName.Equals(current.Name) && current.Active) {
					return true;
				}
			}
			return false;
		}
	}
}