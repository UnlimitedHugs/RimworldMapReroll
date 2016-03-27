using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace MapReroll {
	public class ModInitializer : ITab {
		
		private static GameObject obj;

		public ModInitializer(){
			if (obj != null) return;
			obj = new GameObject("MapRerollLoader");
			obj.AddComponent<ModInitializerComponent>();
			Object.DontDestroyOnLoad(obj);
			Log.Message("MapReroll initialized.");
		}
		
		protected override void FillTab() {			
		}
	}
}
