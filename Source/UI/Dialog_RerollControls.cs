using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MapReroll.UI {
	public class Dialog_RerollControls : Window {
		private const float ContentsPadding = 15f;

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
			var rerollState = RerollToolbox.GetStateForMap();
			float diceButtonSize = MapRerollController.Instance.WidgetSizeSetting;
			var buttonRect = new Rect((inRect.width - diceButtonSize) + Margin, -Margin, diceButtonSize, diceButtonSize);
			if (Widgets.ButtonImage(buttonRect, Resources.Textures.UIDiceActive)) {
				Close();
			}
			var contentsRect = new Rect(inRect.x + ContentsPadding, inRect.y + ContentsPadding, inRect.width - ContentsPadding*2, inRect.height - ContentsPadding*2);
			Text.Anchor = TextAnchor.MiddleCenter;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(contentsRect.x, contentsRect.y, contentsRect.width, 30f), "MapReroll_windowTitle".Translate());
			Text.Font = GameFont.Small;
			var paidRerolls = MapRerollController.Instance.PaidRerollsSetting;

			var resourcesLabelText = paidRerolls ? "MapReroll_oresLeft".Translate(rerollState == null ? 0 : rerollState.ResourceBalance) : "MapReroll_freeRerollsLabel".Translate();
			Widgets.Label(new Rect(contentsRect.x, contentsRect.y + 40f, contentsRect.width, 25f), resourcesLabelText);
			Text.Anchor = TextAnchor.UpperLeft;
			var mapCostSuffix = "MapReroll_resourceCost_suffix".Translate(RerollToolbox.GetOperationCost(PaidOperationType.GeneratePreviews));
			var rerollMapLabel = "MapReroll_rerollMapBtn".Translate(paidRerolls ? mapCostSuffix : "");
			var rerollMapBtnRect = new Rect(contentsRect.x, contentsRect.y + 80f, contentsRect.width, 40f);
			if (Widgets.ButtonText(rerollMapBtnRect, rerollMapLabel)) {
				if (RerollToolbox.GetOperationCost(PaidOperationType.GeneratePreviews) > 0) {
					RerollToolbox.ChargeForOperation(PaidOperationType.GeneratePreviews);
				}
				Find.WindowStack.Add(new Dialog_MapPreviews());
			}
			var geyserCostSuffix = "MapReroll_resourceCost_suffix".Translate(RerollToolbox.GetOperationCost(PaidOperationType.RerollGeysers));
			var geyserRerollLabel = "MapReroll_rerollGeysersBtn".Translate(paidRerolls ? geyserCostSuffix : "");
			if(Widgets.ButtonText(new Rect(contentsRect.x, contentsRect.y + 125f, contentsRect.width, 40f), geyserRerollLabel)){
				if (!MapRerollController.Instance.GeyserRerollInProgress) {
					MapRerollController.Instance.RerollGeysers();
				} else {
					Messages.Message("Reroll2_rerollInProgress".Translate(), MessageSound.RejectInput);
				}
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
	}
}
