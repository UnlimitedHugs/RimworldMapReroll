using RimWorld;
using UnityEngine;
using Verse;

namespace MapReroll {
	[StaticConstructorOnStartup]
	public class RerollGUIWidget {
		public const float WidgetMargin = 10f;

		private readonly Texture2D UITex_OpenRerollDialog = ContentFinder<Texture2D>.Get("icon_inactive", false);

		public static Rect GetWidgetRect() {
			var widgetOffset = GetTutorOffset();
			var widgetSize = MapRerollController.Instance.WidgetSize * Prefs.UIScale;
			return new Rect(widgetOffset.x + (Screen.width - WidgetMargin - widgetSize), widgetOffset.y + WidgetMargin, widgetSize, widgetSize);
		}

		private static Vector2 GetTutorOffset() {
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
			tutorWindowWidth *= Prefs.UIScale;
			tutorMargin *= Prefs.UIScale;
			return tutorWindowWidth > 0 ? new Vector2(-(tutorWindowWidth + tutorMargin), 0) : Vector2.zero;
		}

		public void Initialize(bool mapWasRerolled) {
			if (mapWasRerolled) {
				Find.WindowStack.Add(new Dialog_RerollControls());
			}
		}

		public void OnGUI() {
			if(!MapRerollController.Instance.ShowInterface || Find.TickManager.TicksGame < 1) return;
			var buttonRect = GetWidgetRect();
			if (Widgets.ButtonImage(buttonRect, UITex_OpenRerollDialog)) {
				Find.WindowStack.Add(new Dialog_RerollControls());
			}
		}
	}
}
