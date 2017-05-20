using UnityEngine;
using Verse;

namespace MapReroll {
	[StaticConstructorOnStartup]
	public class RerollGUIWidget {
		private static readonly Texture2D UITex_OpenRerollDialog;

		static RerollGUIWidget() {
			UITex_OpenRerollDialog = ContentFinder<Texture2D>.Get("icon_inactive", false);
		}

		public void OnGUI() {
			var buttonRect = MapRerollUIController.GetWidgetRect();
			if (Widgets.ButtonImage(buttonRect, UITex_OpenRerollDialog)) {
				Find.WindowStack.Add(new Dialog_RerollControls());
			}
		}
	}
}
