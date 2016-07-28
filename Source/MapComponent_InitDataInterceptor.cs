using Verse;

namespace MapReroll {
	/**
	* This is a sneaky way to capture the GameInitData of a generating map, before its only reference is nulled.
	* It self-removes from the components list so as to not leave an unnecessary trace in the save file.
	*/
	public class MapComponent_InitDataInterceptor : MapComponent {
		public MapComponent_InitDataInterceptor() {
			MapRerollController.Instance.SetCapturedInitData(Current.Game.InitData);
		}

		public override void MapComponentTick() {
			base.MapComponentTick();
			Find.Map.components.Remove(this);
		}
	}
}