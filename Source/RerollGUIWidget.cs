using RimWorld;
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
			float tutorMargin = 0;
			float tutorWindowWidth = 0;
			if (Find.Tutor.activeLesson.ActiveLessonVisible) {
				// active tutorial mode
				tutorWindowWidth = 310f;
				tutorMargin = 16f;
			} else if(TutorSystem.AdaptiveTrainingEnabled && (Find.PlaySettings.showLearningHelper || Find.Tutor.learningReadout.ActiveConceptsCount>0)){
				// concept panel
				tutorWindowWidth = 200f;
				tutorMargin = 8f;
			}

			return tutorWindowWidth > 0 ? new Vector2(-(tutorWindowWidth + tutorMargin), 0) : Vector2.zero;
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
			if(!MapRerollController.Instance.ShowInterface || Find.TickManager.TicksGame < 1) return;
			var widgetOffset = MapRerollController.Instance.SettingsDef.interfaceOffset + GetTutorOffset();
			var buttonRect = new Rect(widgetOffset.x + (Screen.width - WidgetMargin - WidgetSize), widgetOffset.y + WidgetMargin, WidgetSize, WidgetSize);
			if (Widgets.ButtonImage(buttonRect, UITex_OpenRerollDialog)) {
				Find.WindowStack.Add(dialogWindow);
			}
		}
	}
}
