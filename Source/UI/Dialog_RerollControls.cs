using System;
using RimWorld;
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
			layer = WindowLayer.SubSuper;
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
				return new Vector2(320f, 360f);
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
			// close on world map, on committed maps
			var mapState = RerollToolbox.GetStateForMap();
			if (Find.World.renderer.wantedMode != WorldRenderMode.None || Find.VisibleMap == null || mapState.MapCommitted) {
				Close();
				return;
			}
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
			Text.Anchor = TextAnchor.UpperLeft;
			
			var layoutRect = new Rect(contentsRect.x, contentsRect.y + headerRect.yMax, contentsRect.width, contentsRect.height - headerRect.yMax);
			GUILayout.BeginArea(layoutRect);
			GUILayout.BeginVertical();
			var extraResourcesHeight = 0f;
			var separatorHeight = ContentsPadding+1;
			var controlHeight = (layoutRect.height - (ControlSpacing * 2f + separatorHeight + extraResourcesHeight)) / 4f;
			
			balanceWidget.DrawLayout(controlHeight + extraResourcesHeight);

			GUILayout.Space(ControlSpacing);
			DoRerollTabButton(Resources.Textures.UIRerollMapOn, Resources.Textures.UIRerollMapOff, MapRerollUtility.WithCostSuffix("Reroll2_rerollMap", PaidOperationType.GeneratePreviews), null, controlHeight, () => {
				if (RerollToolbox.GetOperationCost(PaidOperationType.GeneratePreviews) > 0) {
					RerollToolbox.ChargeForOperation(PaidOperationType.GeneratePreviews);
				}
				Find.WindowStack.Add(new Dialog_MapPreviews());
			});

			GUILayout.Space(ControlSpacing);
			DoRerollTabButton(Resources.Textures.UIRerollGeysersOn, Resources.Textures.UIRerollGeysersOff, MapRerollUtility.WithCostSuffix("Reroll2_rerollGeysers", PaidOperationType.RerollGeysers), null, controlHeight, () => {
				if (!MapRerollController.Instance.GeyserRerollInProgress) {
					MapRerollController.Instance.RerollGeysers();
				} else {
					Messages.Message("Reroll2_rerollInProgress".Translate(), MessageTypeDefOf.RejectInput);
				}
			});

			DrawSeparator(separatorHeight);

			DoRerollTabButton(Resources.Textures.UICommitMapOn, Resources.Textures.UICommitMapOff, MapRerollUtility.WithCostSuffix("Reroll2_commitMap", PaidOperationType.RerollGeysers), "Reroll2_commitMap_tip".Translate(), controlHeight, () => {
				RerollToolbox.GetStateForMap().MapCommitted = true;
				MapRerollController.Instance.UIController.ResetCache();
			});
			GUILayout.EndVertical();
			GUILayout.EndArea();
		}

		// ensure the window is always in the right position over the dice button
		public override void Notify_ResolutionChanged() {
			ResetPosition();
		}

		private void ResetPosition() {
			var widgetRect = MapRerollUIController.GetWidgetRect();
			windowRect = new Rect(widgetRect.x + widgetRect.width - InitialSize.x, widgetRect.y, InitialSize.x, InitialSize.y);
		}

		private void DrawSeparator(float height) {
			var prevColor = GUI.color;
			GUI.color = buttonOutlineColorNormal;
			GUILayout.Box(new GUIContent(), Widgets.EmptyStyle, GUILayout.Height(height));
			var lineRect = GUILayoutUtility.GetLastRect().ContractedBy(ControlSpacing);
			Widgets.DrawLineHorizontal(lineRect.x, lineRect.center.y-1, lineRect.width);
			GUI.color = prevColor;
		}

		private void DoRerollTabButton(Texture2D iconOn, Texture2D iconOff, string label, string tooltip, float controlHeight, Action callback) {
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
			var icon = (hovering ? iconOn : iconOff) ?? BaseContent.BadTex;
			const float iconScale = .75f;
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
