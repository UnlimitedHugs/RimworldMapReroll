using UnityEngine;
using Verse;

namespace MapReroll {
	public class ModInitializerComponent : MonoBehaviour {
		private bool initScheduled;
		private PrepareCarefullyCompat prepareCompat;

		public void Start() {
			if(MapRerollUtility.IsModActive(PrepareCarefullyCompat.ModName)) {
				prepareCompat = new PrepareCarefullyCompat();
				prepareCompat.Initialize();
			}
		}

		public void FixedUpdate() {
			if (!initScheduled || Game.Mode != GameMode.MapPlaying || Find.WindowStack == null) return;
			initScheduled = false;
			MapRerollController.Instance.Notify_OnLevelLoaded();
			RerollGUIWidget.Instance.Initialize();
		}

		public void OnLevelWasLoaded(int level){
			if(MapRerollUtility.IsModActive(MapRerollController.ModName)) {
				initScheduled = true;
			}
		}

		public void OnGUI() {
			RerollGUIWidget.Instance.OnGUI();
		}
	}
}
