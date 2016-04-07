using System.Reflection;
using UnityEngine;
using Verse;

namespace MapReroll {
	public class ModInitializerComponent : MonoBehaviour {
		private bool initScheduled;
		private PrepareCarefullyCompat prepareCompat;
		
		public void Start() {
			// Compat off for now, waiting for EdB to update
			/*if(MapRerollUtility.IsModActive(PrepareCarefullyCompat.ModName)) {
				prepareCompat = new PrepareCarefullyCompat();
				prepareCompat.Initialize();
			}*/
		}

		public void FixedUpdate() {
			//RegionAndRoomUpdater.Enabled ensures we are executing after MapIniterUtility.FinalizeMapInit()
			if (!initScheduled || Game.Mode != GameMode.MapPlaying || !RegionAndRoomUpdater.Enabled) return;
			initScheduled = false;
			LongEventHandler.ExecuteWhenFinished(() => { // this should execute after mapDrawer is initialized
				MapRerollController.Instance.Notify_OnLevelLoaded();
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
