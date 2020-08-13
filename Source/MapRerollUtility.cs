using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MapReroll {
	public static class MapRerollUtility {
		private const int MaxLoops = 100000;
		
		private static int loopCount;
		private static int lastSeenFrame;

		public static void DrawWithGUIColor(Color color, Action drawAction) {
			var prevColor = GUI.color;
			GUI.color = color;
			drawAction();
			GUI.color = prevColor;
		}

		public static string WithCostSuffix(string translationKey, PaidOperationType type, int desiredPreviewsPage = 0) {
			var cost = RerollToolbox.GetOperationCost(type, desiredPreviewsPage);
			var suffix = cost > 0 ? "Reroll2_costSuffix".Translate(cost.ToString("0.#")).ToString() : String.Empty;
			return translationKey.Translate(suffix);
		}

		public static void LoopSafety(int loopLimit = MaxLoops) {
			var frame = Time.frameCount;
			if (lastSeenFrame != frame) {
				lastSeenFrame = frame;
				loopCount = 0;
			}
			loopCount++;
			if (loopCount > loopLimit) {
				throw new Exception("Infinite loop detected");
			}
		}

		public static IEnumerable<T> ExceptNull<T>(this IEnumerable<T> source) {
			foreach (var item in source) {
				if (item != null) yield return item;
			}
		}
		
		// button with improved disabled state. The stock button still reacts to mouse events when disabled.
		internal static bool DrawActiveButton(Rect btnRect, string label, string tooltipKey, bool active) {
			var pressed = false;
			if (active) {
				pressed = Widgets.ButtonText(btnRect, label);
			} else {
				DrawDisabledButton(btnRect, label);
			}
			if (Mouse.IsOver(btnRect)) TooltipHandler.TipRegion(btnRect, tooltipKey.Translate());
			return pressed;
		}

		private static void DrawDisabledButton(Rect btnRect, string label) {
			var prevColor = GUI.color;
			GUI.color = new Color(0.37f, 0.37f, 0.37f, 0.8f);
			Widgets.DrawAtlas(btnRect, Resources.Textures.ButtonAtlas);
			var prevAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(btnRect, label);
			Text.Anchor = prevAnchor;
			GUI.color = prevColor;
		}
	}
}