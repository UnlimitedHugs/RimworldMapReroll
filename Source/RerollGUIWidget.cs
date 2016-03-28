using UnityEngine;
using Verse;

namespace MapReroll {
	public static class RerollGUIWidget {
		private const float WidgetMargin = 10f;
		private const float WidgetSize = 64f;

		private static Texture2D UITex_OpenRerollDialog;
		private static Dialog_RerollControls dialogWindow;

		public static void Initialize() {
			UITex_OpenRerollDialog = ContentFinder<Texture2D>.Get("icon_inactive", false);
			dialogWindow = new Dialog_RerollControls();
		}

		public static void OnGUI() {
			if(!MapRerollController.ShowInterface) return;
			var buttonRect = new Rect(Screen.width - WidgetMargin - WidgetSize, WidgetMargin, WidgetSize, WidgetSize);
			if (Widgets.ImageButton(buttonRect, UITex_OpenRerollDialog)) {
				Find.WindowStack.Add(dialogWindow);
			}
		}
	}
}
