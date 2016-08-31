using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MapReroll {
	[StaticConstructorOnStartup]
	public class Dialog_RerollControls : Window {
		private const float ContentsPadding = 15f;
		private static readonly Texture2D UITex_CloseDialogDice = ContentFinder<Texture2D>.Get("icon_active", false);

		private static readonly SoundDef steamSound = SoundDef.Named("RerollSteamVent");
		private static readonly SoundDef diceSound = SoundDef.Named("RerollDiceRoll");
		private const float diceSoundDuration = .7f;

		private float mapRerollTimeout;

		public Dialog_RerollControls() {
			closeOnEscapeKey = true;
			doCloseButton = false;
			doCloseX = false;
			absorbInputAroundWindow = false;
			closeOnClickedOutside = false;
			preventCameraMotion = false;
			forcePause = false;
			resizeable = false;
			draggable = false;
		}

		public override Vector2 InitialSize {
			get {
				return new Vector2(350f, 195f);
			}
		}

		protected override float Margin {
			get {
				return 0;
			}
		}

		public override void PostOpen() {
			const float windowMargin = RerollGUIWidget.WidgetMargin;
			var widgetOffset = MapRerollController.Instance.SettingsDef.interfaceOffset + RerollGUIWidget.GetTutorOffset();
			windowRect = new Rect(widgetOffset.x + (Screen.width - InitialSize.x - windowMargin), widgetOffset.y + windowMargin, InitialSize.x, InitialSize.y);
		}

		public override void DoWindowContents(Rect inRect) {
			float diceButtonSize = RerollGUIWidget.WidgetSize;
			var buttonRect = new Rect((inRect.width - diceButtonSize) + Margin, -Margin, diceButtonSize, diceButtonSize);
			if (Widgets.ButtonImage(buttonRect, UITex_CloseDialogDice)) {
				Close();
			}
			var contentsRect = new Rect(inRect.x + ContentsPadding, inRect.y + ContentsPadding, inRect.width - ContentsPadding * 2, inRect.height - ContentsPadding * 2);
			Text.Anchor = TextAnchor.MiddleCenter;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(contentsRect.x, contentsRect.y, contentsRect.width, 30f), "MapReroll_windowTitle".Translate());
			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(contentsRect.x, contentsRect.y + 40f, contentsRect.width, 25f), String.Format("MapReroll_oresLeft".Translate(), MapRerollController.Instance.ResourcePercentageRemaining));
			Text.Anchor = TextAnchor.UpperLeft;
			var rerollMapHit = Widgets.ButtonText(new Rect(contentsRect.x, contentsRect.y + 80f, contentsRect.width, 40f), String.Format("MapReroll_rerollMapBtn".Translate(), MapRerollController.Instance.SettingsDef.mapRerollCost));
			var rerollGeysersHit = Widgets.ButtonText(new Rect(contentsRect.x, contentsRect.y + 125f, contentsRect.width, 40f), String.Format("MapReroll_rerollGeysersBtn".Translate(), MapRerollController.Instance.SettingsDef.geyserRerollCost));
			if (rerollMapHit && CanAffordOperation(MapRerollController.MapRerollType.Map) && mapRerollTimeout==0) {
				diceSound.PlayOneShotOnCamera();
				mapRerollTimeout = Time.time + diceSoundDuration;
			}
			if(rerollGeysersHit && CanAffordOperation(MapRerollController.MapRerollType.Geyser)) {
				steamSound.PlayOneShotOnCamera();
				MapRerollController.Instance.RerollGeysers();
			}
			// give an extra moment for the button sound to finish playing
			if(mapRerollTimeout!=0 && Time.time>=mapRerollTimeout){
				mapRerollTimeout = 0;
				MapRerollController.Instance.RerollMap();
			}
		}

		private bool CanAffordOperation(MapRerollController.MapRerollType operation) {
			if(!MapRerollController.Instance.CanAffordOperation(operation)) {
				SoundDefOf.ClickReject.PlayOneShotOnCamera();
				return false;
			}
			return true;
		}
	}
}
