using UnityEngine;
using Verse;
using Object = UnityEngine.Object;
using System.Linq;

namespace MapReroll {
	public class ModInitializer : ITab {
		
		private static GameObject obj;

		public ModInitializer(){
			if (obj != null) return;
			obj = new GameObject("MapRerollLoader");
			obj.AddComponent<ModInitializerComponent>();
			Object.DontDestroyOnLoad(obj);
			Log.Message("MapReroll initialized.");
			if(DefDatabase<MapGeneratorDef>.AllDefs.ToArray().Length>1) {
				Log.Warning("[MapReroll] There is more than one MapGeneratorDef in the database. MapReroll cannot guarantee consistent behaviour.");
			}
		}
		
		protected override void FillTab() {			
		}
	}
}
