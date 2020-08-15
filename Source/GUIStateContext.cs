using System;
using UnityEngine;
using Verse;

namespace MapReroll {
	/// <summary>
	/// A convenient way to apply GUI color, text anchor and font with the using statement.
	/// Despite implementing IDisposable, the struct will not be boxed due to compiler optimization.
	/// </summary>
	internal readonly struct GUIStateContext : IDisposable {
		public static GUIStateContext Set(Color? color = null, TextAnchor? anchor = null, GameFont? font = null) {
			var ctx = new GUIStateContext(color != null, anchor != null, font != null); 
			if (color != null) GUI.color = color.Value;
			if (anchor != null) Text.Anchor = anchor.Value;
			if (font != null) Text.Font = font.Value;
			return ctx;
		}
		
		
		private readonly Color? originalColor;
		private readonly TextAnchor? originalAnchor;
		private readonly GameFont? originalFont;

		public GUIStateContext(bool color, bool anchor, bool font) {
			originalColor = color ? (Color?)GUI.color : null;
			originalAnchor = anchor ? (TextAnchor?)Text.Anchor : null;
			originalFont = font ? (GameFont?)Text.Font : null;
		}

		public void Dispose() {
			if (originalColor != null) GUI.color = originalColor.Value;
			if (originalAnchor != null) Text.Anchor = originalAnchor.Value;
			if (originalFont != null) Text.Font = originalFont.Value;
		}
	}
}