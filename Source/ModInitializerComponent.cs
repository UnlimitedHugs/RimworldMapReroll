using UnityEngine;
using Verse;

namespace MapReroll {
	public class ModInitializerComponent : MonoBehaviour {
		private static bool initPerformed;

		public void FixedUpdate() {
			if (initPerformed || Game.Mode != GameMode.MapPlaying || Find.WindowStack == null) return;
			MapRerollController.OnLevelLoaded();
			RerollGUIWidget.Initialize();
			initPerformed = true;
		}

		public void OnLevelWasLoaded() {
			initPerformed = false;
		}

		public void OnGUI() {
			if(initPerformed) RerollGUIWidget.OnGUI();
		}
	}
}
