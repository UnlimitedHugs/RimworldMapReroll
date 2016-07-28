using UnityEngine;
using Verse;

namespace MapReroll {
	public class RerollGUIWidget {
		public const float WidgetMargin = 10f;
		public static float WidgetSize = 48f;

		private static RerollGUIWidget instance;
		public static RerollGUIWidget Instance {
			get {
				return instance ?? (instance = new RerollGUIWidget());
			}
		}

		private Texture2D UITex_OpenRerollDialog;
		private Dialog_RerollControls dialogWindow;
		private bool firstInit = true;


		public void Initialize() {
			if(firstInit) {
				UITex_OpenRerollDialog = ContentFinder<Texture2D>.Get("icon_inactive", false);
				dialogWindow = new Dialog_RerollControls();
				MapRerollController.Instance.OnMapRerolled += InstanceOnOnMapRerolled;
				firstInit = false;
			}
			WidgetSize = MapRerollController.Instance.SettingsDef.diceWidgetSize;
		}

		private void InstanceOnOnMapRerolled() {
			Find.WindowStack.Add(dialogWindow);
		}

		public void OnGUI() {
			if(!MapRerollController.Instance.ShowInterface) return;
			var buttonRect = new Rect(Screen.width - WidgetMargin - WidgetSize, WidgetMargin, WidgetSize, WidgetSize);
			if (Widgets.ButtonImage(buttonRect, UITex_OpenRerollDialog)) {
				Find.WindowStack.Add(dialogWindow);
			}
		}
	}
}
