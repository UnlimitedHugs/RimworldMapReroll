using MapReroll.Interpolation;
using UnityEngine;
using Verse;

namespace MapReroll.UI {
	public class Widget_ResourceBalance {
		private const float ControlPadding = 6f;
		private const float InterpolationDuration = 2f;
		private readonly Color outlineColor = GenColor.FromHex("1D4B6E");

		private ValueInterpolator interpolator;
		private float lastSeenBalance;

		public Widget_ResourceBalance(float customStartValue = -1) {
			var map = Find.VisibleMap;
			var startValue = customStartValue >= 0 ? customStartValue : (map != null ? GetResourceBalance(map) : 100);
			lastSeenBalance = startValue;
			interpolator = new ValueInterpolator(startValue);
		}
		
		public void DrawLayout() {
			var map = Find.VisibleMap;
			if (map == null) return;
			UpdateInterpolator(map);
			GUILayout.Box(string.Empty, Widgets.EmptyStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			var controlRect = GUILayoutUtility.GetLastRect();
			var contentsRect = controlRect.ContractedBy(ControlPadding);

			var prevColor = GUI.color;
			GUI.color = outlineColor;
			Widgets.DrawBox(controlRect);
			GUI.color = prevColor;

			var labelText = "Reroll2_remainingResources".Translate();
			var labelHeight = Text.CalcHeight(labelText, contentsRect.width);
			var labelRect = new Rect(contentsRect.x, contentsRect.y, contentsRect.width, labelHeight);
			Widgets.Label(labelRect, labelText);
			var barYOffset = contentsRect.y + labelRect.yMax;
			var barRect = new Rect(contentsRect.x, barYOffset, contentsRect.width, contentsRect.height - barYOffset + ControlPadding);
			float fillPercent = Mathf.Clamp(interpolator.value, 0, MapRerollController.MaxResourceBalance);
			Widgets.FillableBar(barRect, fillPercent/MapRerollController.MaxResourceBalance, Resources.Textures.ResourceBarFull, Resources.Textures.ResourceBarEmpty, false);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(barRect, string.Format("{0:F1}%", interpolator.value));
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private float GetResourceBalance(Map map) {
			return RerollToolbox.GetStateForMap(map).ResourceBalance;
		}

		private void UpdateInterpolator(Map map) {
			if (Event.current.type != EventType.Repaint) return;
			var balance = GetResourceBalance(map);
			interpolator.Update();
			if (balance != lastSeenBalance) {
				lastSeenBalance = balance;
				interpolator.StartInterpolation(balance, InterpolationDuration, CurveType.CubicInOut);
			}
		}
	}
}
