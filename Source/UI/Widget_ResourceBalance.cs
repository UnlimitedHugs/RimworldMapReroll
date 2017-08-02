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
		
		public void DrawLayout(float height) {
			var map = Find.VisibleMap;
			if (map == null) return;
			UpdateInterpolator(map);
			GUILayout.Box(string.Empty, Widgets.EmptyStyle, GUILayout.ExpandWidth(true), GUILayout.Height(height));
			var controlRect = GUILayoutUtility.GetLastRect();
			var contentsRect = controlRect.ContractedBy(ControlPadding);
			var labelHeight = 25f;

			DrawColoredOutline(outlineColor, controlRect);

			var bottomHalf = new Rect(contentsRect.x, contentsRect.y + labelHeight, contentsRect.width, contentsRect.height - labelHeight);
			if (MapRerollController.Instance.PaidRerollsSetting) {
				Widgets.Label(contentsRect, "Reroll2_remainingResources".Translate(interpolator.value));
				float fillPercent = Mathf.Clamp(interpolator.value, 0, MapRerollController.MaxResourceBalance);
				DrawTiledTexture(bottomHalf, Resources.Textures.UISteelBack);
				var barRect = new Rect(bottomHalf.x, bottomHalf.y, bottomHalf.width * (fillPercent / 100f), bottomHalf.height);
				DrawTiledTexture(barRect, Resources.Textures.UISteelFront);
			} else {
				Widgets.Label(contentsRect, "MapReroll_freeRerollsLabel".Translate());
				GUI.DrawTexture(bottomHalf, Resources.Textures.ResourceBarFull);
			}

			DrawColoredOutline(Color.grey, bottomHalf);
		}

		private void DrawColoredOutline(Color color, Rect rect) {
			var prevColor = GUI.color;
			GUI.color = color;
			Widgets.DrawBox(rect);
			GUI.color = prevColor;
		}

		private void DrawTiledTexture(Rect rect, Texture2D tex) {
			GUI.DrawTextureWithTexCoords(rect, tex, new Rect(0, 0, rect.width / tex.width, rect.height / tex.height));
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
