using System;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MapReroll.UI {
	public class Dialog_RerollControls : Window {
		private const float ContentsPadding = 15f;
		private const float ControlPadding = 6f;
		private const float ControlSpacing = 6f;
		private readonly Color buttonOutlineColorNormal = GenColor.FromHex("1D4B6E");
		private readonly Color buttonOutlineColorHover = GenColor.FromHex("616C7A");

		private Widget_ResourceBalance balanceWidget;

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
				return new Vector2(350f, 260f);
			}
		}

		protected override float Margin {
			get {
				return 0;
			}
		}

		public override void PreOpen() {
			var map = Find.VisibleMap;
			var resourceBalance = map == null ? 0f : RerollToolbox.GetStateForMap(map).ResourceBalance;
			balanceWidget = new Widget_ResourceBalance(resourceBalance);
		}

		public override void PostOpen() {
			ResetPosition();
		}
		
		public override void DoWindowContents(Rect inRect) {
			float diceButtonSize = MapRerollController.Instance.WidgetSizeSetting;
			var buttonRect = new Rect((inRect.width - diceButtonSize) + Margin, -Margin, diceButtonSize, diceButtonSize);
			if (Widgets.ButtonImage(buttonRect, Resources.Textures.UIDiceActive)) {
				Close();
			}
			var contentsRect = inRect.ContractedBy(ContentsPadding);
			Text.Anchor = TextAnchor.MiddleCenter;
			Text.Font = GameFont.Medium;
			var headerRect = new Rect(contentsRect.x, contentsRect.y, contentsRect.width, 30f);
			Widgets.Label(headerRect, "MapReroll_windowTitle".Translate());
			Text.Font = GameFont.Small;
			
			var layoutRect = new Rect(contentsRect.x, contentsRect.y + headerRect.yMax, contentsRect.width, contentsRect.height - headerRect.yMax);
			GUILayout.BeginArea(layoutRect);
			GUILayout.BeginVertical();
			var controlHeight = (layoutRect.height - ControlSpacing * 2f) / 3f;
			balanceWidget.DrawLayout(controlHeight);
			GUILayout.Space(ControlSpacing);
			DoRerollTabButton(Resources.Textures.UIRerollMap, MapRerollUtility.WithCostSuffix("Reroll2_rerollMap", PaidOperationType.GeneratePreviews), null, controlHeight, () => {
				if (RerollToolbox.GetOperationCost(PaidOperationType.GeneratePreviews) > 0) {
					RerollToolbox.ChargeForOperation(PaidOperationType.GeneratePreviews);
				}
				Find.WindowStack.Add(new Dialog_MapPreviews());
			});
			GUILayout.Space(ControlSpacing);
			DoRerollTabButton(Resources.Textures.UIRerollGeysers, MapRerollUtility.WithCostSuffix("Reroll2_rerollGeysers", PaidOperationType.RerollGeysers), null, controlHeight, () => {
				if (!MapRerollController.Instance.GeyserRerollInProgress) {
					MapRerollController.Instance.RerollGeysers();
				} else {
					Messages.Message("Reroll2_rerollInProgress".Translate(), MessageSound.RejectInput);
				}
			});
			GUILayout.EndVertical();
			GUILayout.EndArea();
			
			/*var rerollState = RerollToolbox.GetStateForMap();
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
			}*/
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

		private void DoRerollTabButton(Texture2D icon, string label, string tooltip, float controlHeight, Action callback) {
			var prevColor = GUI.color;
			if (GUILayout.Button(string.Empty, Widgets.EmptyStyle, GUILayout.ExpandWidth(true), GUILayout.Height(controlHeight))) {
				callback();
			}
			var controlRect = GUILayoutUtility.GetLastRect();
			var contentsRect = controlRect.ContractedBy(ControlPadding);

			var hovering = Mouse.IsOver(controlRect);
			GUI.color = hovering ? buttonOutlineColorHover : buttonOutlineColorNormal;
			Widgets.DrawBox(controlRect);
			GUI.color = prevColor;
			if (icon == null) {
				icon = BaseContent.BadTex;
			}

			var iconScale = .75f;
			var iconSize = new Vector2(64f, 64f) * iconScale;
			var iconRect = new Rect(contentsRect.x, contentsRect.y + contentsRect.height / 2f - iconSize.y / 2f, iconSize.x, iconSize.y);
			Widgets.DrawTextureFitted(iconRect, icon, 1f);

			var labelRect = new Rect(iconRect.xMax + ControlPadding, contentsRect.y, contentsRect.width - (iconRect.width + ControlPadding * 3), contentsRect.height);
			var prevAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(labelRect, label);
			Text.Anchor = prevAnchor;
			if (hovering && !tooltip.NullOrEmpty()) {
				TooltipHandler.TipRegion(controlRect, tooltip);
			}
		}
	}
}
