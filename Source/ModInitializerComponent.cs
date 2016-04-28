using UnityEngine;
using Verse;

namespace MapReroll {
	public class ModInitializerComponent : MonoBehaviour {
		private bool initScheduled;
		private PrepareCarefullyCompat prepareCompat;

		public void FixedUpdate() {
			//RegionAndRoomUpdater.Enabled ensures we are executing after MapIniterUtility.FinalizeMapInit()
			if (!initScheduled || Game.Mode != GameMode.MapPlaying || !RegionAndRoomUpdater.Enabled) return;
			initScheduled = false;
			LongEventHandler.ExecuteWhenFinished(() => { // this should execute after mapDrawer is initialized
				if (prepareCompat == null && MapRerollUtility.IsModActive(PrepareCarefullyCompat.ModName)) {
					prepareCompat = new PrepareCarefullyCompat();
				}
				MapRerollController.Instance.Notify_OnLevelLoaded();
				if (prepareCompat != null) prepareCompat.Notify_OnLevelLoaded();
				RerollGUIWidget.Instance.Initialize();
			});
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
