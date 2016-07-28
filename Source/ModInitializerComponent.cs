using UnityEngine;
using Verse;

namespace MapReroll {
	public class ModInitializerComponent : MonoBehaviour {
		private bool initScheduled;

		public void FixedUpdate() {
			//RegionAndRoomUpdater.Enabled ensures we are executing after MapIniterUtility.FinalizeMapInit()
			if (!initScheduled || Current.ProgramState != ProgramState.MapPlaying || !RegionAndRoomUpdater.Enabled) return;
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
