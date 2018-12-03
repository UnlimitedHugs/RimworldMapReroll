using System;
using System.Text;
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
			var suffix = cost > 0 ? "Reroll2_costSuffix".Translate(cost.ToString("0.#")) : String.Empty;
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

		public static string Base64Encode(string plainText) {
			var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
			return Convert.ToBase64String(plainTextBytes);
		}

		public static bool TryBase64Decode(string base64EncodedData, out string decodedString, out string errorMessage) {
			errorMessage = null;
			decodedString = null;
			try {
				var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
				decodedString = Encoding.UTF8.GetString(base64EncodedBytes);
				return true;
			} catch (Exception e) {
				errorMessage = e.Message;
			}
			return false;
		}
	}
}