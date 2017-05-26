using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MapReroll {
	[StaticConstructorOnStartup]
	public class Dialog_RerollControls : Window {
		private const float ContentsPadding = 15f;
		private static readonly Texture2D UITex_CloseDialogDice = ContentFinder<Texture2D>.Get("icon_active", false);

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
			ResetPosition();
		}
		
		public override void DoWindowContents(Rect inRect) {
			var rerollState = MapRerollController.Instance.RerollState;
			float diceButtonSize = MapRerollController.Instance.SettingHandles.WidgetSize;
			var buttonRect = new Rect((inRect.width - diceButtonSize) + Margin, -Margin, diceButtonSize, diceButtonSize);
			if (Widgets.ButtonImage(buttonRect, UITex_CloseDialogDice)) {
				Close();
			}
			var contentsRect = new Rect(inRect.x + ContentsPadding, inRect.y + ContentsPadding, inRect.width - ContentsPadding*2, inRect.height - ContentsPadding*2);
			Text.Anchor = TextAnchor.MiddleCenter;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(contentsRect.x, contentsRect.y, contentsRect.width, 30f), "MapReroll_windowTitle".Translate());
			Text.Font = GameFont.Small;
			var paidRerolls = MapRerollController.Instance.SettingHandles.PaidRerolls;

			var resourcesLabelText = paidRerolls ? "MapReroll_oresLeft".Translate(rerollState == null ? 0 : rerollState.ResourcesPercentBalance) : "MapReroll_freeRerollsLabel".Translate();
			Widgets.Label(new Rect(contentsRect.x, contentsRect.y + 40f, contentsRect.width, 25f), resourcesLabelText);
			Text.Anchor = TextAnchor.UpperLeft;
			var mapCostSuffix = "MapReroll_resourceCost_suffix".Translate(MapRerollDefOf.MapRerollSettings.mapRerollCost);
			var rerollMapLabel = "MapReroll_rerollMapBtn".Translate(paidRerolls ? mapCostSuffix : "");
			var rerollMapBtnRect = new Rect(contentsRect.x, contentsRect.y + 80f, contentsRect.width, 40f);
			var rerollMapHit = Widgets.ButtonText(rerollMapBtnRect, rerollMapLabel);
			var geyserCostSuffix = "MapReroll_resourceCost_suffix".Translate(MapRerollDefOf.MapRerollSettings.geyserRerollCost);
			var geyserRerollLabel = "MapReroll_rerollGeysersBtn".Translate(paidRerolls ? geyserCostSuffix : "");
			var rerollGeysersHit = Widgets.ButtonText(new Rect(contentsRect.x, contentsRect.y + 125f, contentsRect.width, 40f), geyserRerollLabel);
			if (Mouse.IsOver(rerollMapBtnRect)) {
				var report = MapRerollController.Instance.CanRerollMap();
				if (!report.Accepted) {
					TooltipHandler.TipRegion(rerollMapBtnRect, report.Reason);
				}
			}
			if (rerollMapHit && CanAffordOperation(MapRerollController.MapRerollType.Map) && mapRerollTimeout == 0) {
				var report = MapRerollController.Instance.CanRerollMap();
				if (report.Accepted) {
					MapRerollDefOf.RerollDiceRoll.PlayOneShotOnCamera();
					mapRerollTimeout = Time.time + diceSoundDuration;
				}
			}
			if (rerollGeysersHit && CanAffordOperation(MapRerollController.MapRerollType.Geyser)) {
				MapRerollDefOf.RerollSteamVent.PlayOneShotOnCamera();
				MapRerollController.Instance.RerollGeysers();
			}
			// give an extra moment for the button sound to finish playing
			if (mapRerollTimeout != 0 && Time.time >= mapRerollTimeout) {
				mapRerollTimeout = 0;
				MapRerollController.Instance.RerollMap();
			}
			// close on world map
			if (Find.World.renderer.wantedMode != WorldRenderMode.None) {
				Close(false);
			}
		}

		// ensure the window is always in the right position over the dice button
		public override void Notify_ResolutionChanged() {
			ResetPosition();
		}

		private void ResetPosition() {
			var widgetRect = MapRerollUIController.GetWidgetRect();
			windowRect = new Rect(widgetRect.x + widgetRect.width - InitialSize.x, widgetRect.y, InitialSize.x, InitialSize.y);
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
