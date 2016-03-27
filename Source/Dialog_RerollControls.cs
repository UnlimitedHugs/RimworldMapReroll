using System;
using UnityEngine;
using Verse;

namespace MapReroll {
	public class Dialog_RerollControls : Window {
		private const float ContentsPadding = 15f;
		private const float DiceButtonSize = 64f;
		private static readonly Texture2D UITex_CloseDialogDice = ContentFinder<Texture2D>.Get("icon_active");

		public Dialog_RerollControls(){
			closeOnEscapeKey = true;
			doCloseButton = false;
			doCloseX = false;
			absorbInputAroundWindow = false;
			forcePause = false;
			resizeable = false;
			draggable = false;
		}

		public override Vector2 InitialWindowSize {
			get {
				return new Vector2(350f, 195f);
			}
		}

		protected override float WindowPadding {
			get {
				return 0;
			}
		}

		public override void PostOpen() {
			currentWindowRect = new Rect(Screen.width - InitialWindowSize.x - 10, 10, InitialWindowSize.x, InitialWindowSize.y);
		}

		public override void DoWindowContents(Rect inRect) {
			var buttonRect = new Rect((inRect.width - DiceButtonSize) + WindowPadding, -WindowPadding, DiceButtonSize, DiceButtonSize);
			if (Widgets.ImageButton(buttonRect, UITex_CloseDialogDice)) {
				Close();
			}
			var contentsRect = new Rect(inRect.x + ContentsPadding, inRect.y + ContentsPadding, inRect.width - ContentsPadding * 2, inRect.height - ContentsPadding * 2);
			Text.Anchor = TextAnchor.MiddleCenter;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(contentsRect.x, contentsRect.y, contentsRect.width, 30f), "MapReroll_windowTitle".Translate());
			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(contentsRect.x, contentsRect.y + 40f, contentsRect.width, 20f), String.Format("MapReroll_oresLeft".Translate(), 0));
			Text.Anchor = TextAnchor.UpperLeft;
			var rerollMapHit = Widgets.TextButton(new Rect(contentsRect.x, contentsRect.y + 80f, contentsRect.width, 40f), String.Format("MapReroll_rerollMapBtn".Translate(), MapRerollController.SettingsDef.mapRerollCost));
			var rerollGeysersHit = Widgets.TextButton(new Rect(contentsRect.x, contentsRect.y + 125f, contentsRect.width, 40f), String.Format("MapReroll_rerollGeysersBtn".Translate(), MapRerollController.SettingsDef.geyserRerollCost));
			if(rerollMapHit) {
				
			}
			if(rerollGeysersHit) {
				
			}
		}
	}
}
