using UnityEngine;

namespace MapReroll.UI {
	public static class Widget_RerollPreloader {
		public static void Draw(Vector2 center, int index) {
			var waveBase = Mathf.Abs(Time.time - index / 2f);
			var wave = Mathf.Sin((Time.time - index / 6f) * 3f);
			var tex = Resources.Textures.UIPreviewLoading;
			var texAlpha = 1f - (1+wave) * .4f;
			var texScale = 1f;
			var rect = new Rect(center.x - (tex.width / 2f) * texScale, center.y - (tex.height / 2f) * texScale, tex.width * texScale, tex.height * texScale);
			var prevColor = GUI.color;
			var baseColor = MapRerollController.Instance.PaidRerollsSetting ? Color.HSVToRGB((waveBase / 16f) % 1f, 1f, 1f) : Color.white;
			GUI.color = new Color(baseColor.r, baseColor.g, baseColor.b, texAlpha);
			GUI.DrawTexture(rect, tex);
			GUI.color = prevColor;
		}
	}
}