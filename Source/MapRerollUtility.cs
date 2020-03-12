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
	}
}