using RimWorld;
using UnityEngine;
using Verse;

namespace MapReroll {
	public class MapRerollUIController {
		public const int DefaultWidgetSize = 48;
		public const int MinWidgetSize = 16;
		public const int MaxWidgetSize = 64;
		private const float WidgetMargin = 10f;

		// rect occupied by the dice button
		public static Rect GetWidgetRect() {
			var widgetOffset = GetTutorOffset();
			var widgetSize = MapRerollController.Instance.SettingHandles.WidgetSize;
			return new Rect(widgetOffset.x + (UI.screenWidth - WidgetMargin - widgetSize), widgetOffset.y + WidgetMargin, widgetSize, widgetSize);
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

		private RerollGUIWidget widget;

		private bool ShowInterface {
			get {
				return Current.ProgramState == ProgramState.Playing
					&& !MapRerollController.Instance.RerollInProgress
					&& MapRerollDefOf.MapRerollSettings.enableInterface
					&& Current.Game != null
					&& Current.Game.VisibleMap != null
					&& MapRerollController.Instance.RerollState != null
					&& MapRerollController.Instance.RerollState.InitData != null
					&& Current.Game.VisibleMap.Tile == MapRerollController.Instance.RerollState.InitData.startingTile
					&& !Faction.OfPlayer.HasName;
			}
		}

		public void MapLoaded(bool mapWasRerolled) {
			if (widget == null) {
				widget = new RerollGUIWidget();
			}
			if (mapWasRerolled) {
				Find.WindowStack.Add(new Dialog_RerollControls());
			}
		}

		public void OnGUI() {
			if (widget != null
			    && ShowInterface
			    && Find.TickManager.TicksGame > 0) {
				
				widget.OnGUI();
			}
		}
	}
}