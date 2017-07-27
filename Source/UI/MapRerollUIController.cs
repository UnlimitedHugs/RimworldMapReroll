using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MapReroll.UI {
	public class MapRerollUIController {
		public const int DefaultWidgetSize = 48;
		public const int MinWidgetSize = 16;
		public const int MaxWidgetSize = 64;
		private const float WidgetMargin = 10f;

		// rect occupied by the dice button
		public static Rect GetWidgetRect() {
			var widgetOffset = GetTutorOffset();
			var widgetSize = MapRerollController.Instance.WidgetSizeSetting;
			return new Rect(widgetOffset.x + (Verse.UI.screenWidth - WidgetMargin - widgetSize), widgetOffset.y + WidgetMargin, widgetSize, widgetSize);
		}

		private static Vector2 GetTutorOffset() {
			float tutorMargin = 0;
			float tutorWindowWidth = 0;
			if (Find.Tutor.activeLesson.ActiveLessonVisible) {
				// active tutorial mode
				tutorWindowWidth = 310f;
				tutorMargin = 16f;
			} else if (TutorSystem.AdaptiveTrainingEnabled && (Find.PlaySettings.showLearningHelper || Find.Tutor.learningReadout.ActiveConceptsCount > 0)) {
				// concept panel
				tutorWindowWidth = 200f;
				tutorMargin = 8f;
			}
			return tutorWindowWidth > 0 ? new Vector2(-(tutorWindowWidth + tutorMargin), 0) : Vector2.zero;
		}

		private bool ShowInterface {
			get {
				return Current.ProgramState == ProgramState.Playing
					&& Current.Game != null
					&& Current.Game.VisibleMap != null
					&& Current.Game.VisibleMap.IsPlayerHome
					&& Find.World.renderer.wantedMode == WorldRenderMode.None
					&& !Faction.OfPlayer.HasName;
			}
		}

		public void MapLoaded(bool mapWasRerolled) {
			if (mapWasRerolled) {
				Find.WindowStack.Add(new Dialog_RerollControls());
			}
		}

		public void OnGUI() {
			if (ShowInterface && Find.TickManager.TicksGame > 0) {
				if (Widgets.ButtonImage(GetWidgetRect(), Resources.Textures.UIDiceInactive)) {
					Find.WindowStack.Add(new Dialog_RerollControls());
				}
			}
		}
	}
}