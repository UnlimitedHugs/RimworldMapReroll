using UnityEngine;
using Verse;

namespace MapReroll {
	public class RerollGUIWidget {
		public const float WidgetMargin = 10f;
		public static float WidgetSize = 48f;

		private static RerollGUIWidget instance;

		public static RerollGUIWidget Instance {
			get { return instance ?? (instance = new RerollGUIWidget()); }
		}

		private Texture2D UITex_OpenRerollDialog;
		private Dialog_RerollControls dialogWindow;
		private bool firstInit = true;

		public static Vector2 GetTutorOffset() {
			if (!Find.Tutor.activeLesson.ActiveLessonVisible) return Vector2.zero;
			const float tutorMargin = 8f;
			const float tutorWidth = 310f;
			return new Vector2(-(tutorWidth + tutorMargin + tutorMargin), 0);
		}

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
			var widgetOffset = MapRerollController.Instance.SettingsDef.interfaceOffset + GetTutorOffset();
			var buttonRect = new Rect(widgetOffset.x + (Screen.width - WidgetMargin - WidgetSize), widgetOffset.y + WidgetMargin, WidgetSize, WidgetSize);
			if (Widgets.ButtonImage(buttonRect, UITex_OpenRerollDialog)) {
				Find.WindowStack.Add(dialogWindow);
			}
		}
	}
}
