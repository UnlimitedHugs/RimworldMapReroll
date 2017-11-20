using Harmony;
using Verse;

namespace MapReroll.Patches {
	/// <summary>
	/// Prevents the Rand state stack from being reset while things are generating in the preview thread
	/// </summary>
	[HarmonyPatch(typeof(Rand), "EnsureStateStackEmpty")]
	internal class Rand_EnsureStateStackEmpty_Patch {
		[HarmonyPrefix]
		public static bool OptionalStackChecks() {
			return !MapRerollController.RandStateStackCheckingPaused;
		}
	}
}