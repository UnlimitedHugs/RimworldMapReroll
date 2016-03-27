using UnityEngine;
using Verse;

namespace MapReroll {
	public static class RerollGUIController {
		private const float WidgetMargin = 10f;
		private const float WidgetSize = 64f;

		private static readonly Texture2D UITex_OpenRerollDialog = ContentFinder<Texture2D>.Get("icon_inactive");
		private static Dialog_RerollControls dialogWindow;

		public static void Initialize() {
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
