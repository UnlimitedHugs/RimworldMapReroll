using System;
using System.Collections.Generic;
using HugsLib;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MapReroll.UI {
	public class Dialog_MapPreviews : Window {
		public const float FavoriteIconScale = .75f;
		private const float ElementSpacing = 10f;

		public static event Action<Dialog_MapPreviews, Rect> DialogOnGUI;

		private static readonly Vector2 PageButtonSize = new Vector2(160f, 40f);
		private static readonly Vector2 GenerateButtonSize = new Vector2(160f, 40f);
		private static readonly Vector2 FavoriteControlSize = new Vector2(160f, Resources.Textures.UIFavoriteStarOn.width * FavoriteIconScale);
		private static readonly Color GenerateButtonColor = new Color(.55f, 1f, .55f);

		private readonly GeneratedPreviewPageProvider previewGenerator;
		private readonly ListPreviewPageProvider favoritesProvider;
		private readonly RerollMapState mapState;

		private List<TabRecord> tabs;
		private TabRecord previewsTab;
		private TabRecord favoritesTab;
		private TabRecord activeTab;
		
		public override Vector2 InitialSize {
			get { return new Vector2(600, 745); }
		}

		public override void Notify_ResolutionChanged() {
			base.Notify_ResolutionChanged();
			SetInitialSizeAndPosition();
		}

		public Dialog_MapPreviews() {
			layer = WindowLayer.SubSuper;
			forcePause = true;
			absorbInputAroundWindow = true;
			draggable = false;
			doCloseX = true;
			doCloseButton = false;
			SetUpTabs();
			mapState = RerollToolbox.GetStateForMap();
			favoritesProvider = new ListPreviewPageProvider();
			previewGenerator = new GeneratedPreviewPageProvider(Find.CurrentMap);
			previewGenerator.OpenPage(0);
			previewGenerator.OnFavoriteToggled = OnPreviewFavoriteToggled;
		}

		public override void PreClose() {
			previewGenerator.Dispose();
			favoritesProvider.Dispose();
		}

		private void SetUpTabs() {
			activeTab = previewsTab = new TabRecord("Reroll2_previews_previewsTab".Translate(), () => OnTabSelected(0), false);
			favoritesTab = new TabRecord(string.Empty, () => OnTabSelected(1), false);
			tabs = new List<TabRecord>{previewsTab, favoritesTab};
		}

		public override void DoWindowContents(Rect inRect) {
			var contentRect = inRect;
			const float tabMargin = 45f;
			contentRect.yMin += tabMargin;
			var bottomSectionHeight = CloseButSize.y;
			var bottomSection = new Rect(inRect.x, inRect.height - bottomSectionHeight, inRect.width, bottomSectionHeight);
			contentRect.yMax -= bottomSection.height+ ElementSpacing;
			for (int i = 0; i < tabs.Count; i++) {
				tabs[i].selected = activeTab == tabs[i];
			}
			favoritesTab.label = "Reroll2_previews_favoritesTab".Translate(favoritesProvider.Count);
			Widgets.DrawMenuSection(contentRect);
			TabDrawer.DrawTabs(contentRect, tabs);
			var tabContentRect = contentRect.ContractedBy(ElementSpacing);
			var bottomBar = new Rect(tabContentRect.x, tabContentRect.yMax - PageButtonSize.y, tabContentRect.width, PageButtonSize.y);
			var previewsArea = new Rect(tabContentRect.x, tabContentRect.y, tabContentRect.width, tabContentRect.height - (bottomBar.height + ElementSpacing));
			if (activeTab == previewsTab) {
				DoPreviewsContents(previewsArea, bottomBar);
			} else if (activeTab == favoritesTab) {
				DoFavoritesContents(previewsArea, bottomBar);
			}
			DoStatusReadout(bottomSection);
			if (Widgets.ButtonText(new Rect(bottomSection.width - CloseButSize.x, bottomSection.yMax - CloseButSize.y, CloseButSize.x, CloseButSize.y), "CloseButton".Translate())) {
				Close();		
			}
			if (DialogOnGUI != null) {
				DialogOnGUI(this, inRect);
			}
		}

		private void DoStatusReadout(Rect inRect) {
			string status;
			if (previewGenerator.NumQueuedPreviews > 0) {
				status = "Reroll2_previews_statusGenerating".Translate(previewGenerator.NumQueuedPreviews, GenText.MarchingEllipsis());
			} else {
				status = "Reroll2_previews_statusComplete".Translate(previewGenerator.PreviewCount);
			}
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(inRect, status);
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private void DoPreviewsContents(Rect previewsArea, Rect bottomBar) {
			previewGenerator.Draw(previewsArea);
			DoBottomBarControls(previewGenerator, bottomBar);
		}

		private void DoFavoritesContents(Rect previewsArea, Rect bottomBar) {
			favoritesProvider.Draw(previewsArea);
			DoBottomBarControls(favoritesProvider, bottomBar);
		}

		private void DoBottomBarControls(BasePreviewPageProvider pageProvider, Rect inRect) {
			var currentZoomedPreview = pageProvider.CurrentZoomedInPreview;
			if (currentZoomedPreview != null) {
				// zoomed in controls
				var generateBtnRect = new Rect(inRect.xMin, inRect.yMin, GenerateButtonSize.x, inRect.height);
				MapRerollUtility.DrawWithGUIColor(GenerateButtonColor, () => {
					if (Widgets.ButtonText(generateBtnRect, "Reroll2_previews_generateMap".Translate())) {
						SoundDefOf.Click.PlayOneShotOnCamera();
						Close();
						HugsLibController.Instance.DoLater.DoNextUpdate(() => {
							previewGenerator.WaitForDisposal();
							MapRerollController.Instance.RerollMap(currentZoomedPreview.Seed);
						});
					}
				});

				var favoritesControlRect = new Rect(generateBtnRect.xMax + ElementSpacing, inRect.yMin, FavoriteControlSize.x, inRect.height);
				var favoriteCheckRect = new Rect(favoritesControlRect.xMin + ElementSpacing, favoritesControlRect.center.y - FavoriteControlSize.y / 2f, FavoriteControlSize.y, FavoriteControlSize.y);
				var checkLabelRect = new Rect(favoriteCheckRect.x + FavoriteControlSize.y + ElementSpacing, favoriteCheckRect.y - 7f, FavoriteControlSize.x, inRect.height);

				if (Widgets.ButtonInvisible(favoritesControlRect)) {
					OnPreviewFavoriteToggled(currentZoomedPreview);
				}
				GUI.DrawTextureWithTexCoords(favoriteCheckRect, currentZoomedPreview.IsFavorite ? Resources.Textures.UIFavoriteStarOn : Resources.Textures.UIFavoriteStarOff, new Rect(0, 0, FavoriteIconScale, FavoriteIconScale));
				if (Mouse.IsOver(favoritesControlRect)) {
					Widgets.DrawHighlight(favoritesControlRect);
				}
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(checkLabelRect, "Reroll2_previews_favoriteCheck".Translate());
				Text.Anchor = TextAnchor.UpperLeft;

				var zoomOutBtnRect = new Rect(inRect.xMax - PageButtonSize.x, inRect.yMin, PageButtonSize.x, inRect.height);
				if (Widgets.ButtonText(zoomOutBtnRect, "Reroll2_previews_zoomOut".Translate())) {
					currentZoomedPreview.ZoomOut();
				}
			} else {
				// paging controls
				var numPagesToTurn = HugsLibUtility.ControlIsHeld ? 5 : 1;
				if (pageProvider.PageIsAvailable(pageProvider.CurrentPage - numPagesToTurn)) {
					if (Widgets.ButtonText(new Rect(inRect.xMin, inRect.yMin, PageButtonSize.x, inRect.height), "Reroll2_previews_prevPage".Translate())) {
						PageBackwards(pageProvider, numPagesToTurn);
					}
				}

				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(inRect, "Page " + (pageProvider.CurrentPage + 1));
				Text.Anchor = TextAnchor.UpperLeft;

				if (pageProvider.PageIsAvailable(pageProvider.CurrentPage + numPagesToTurn)) {
					var paidNextBtnLabel = MapRerollUtility.WithCostSuffix("Reroll2_previews_nextPage", PaidOperationType.GeneratePreviews, pageProvider.CurrentPage + numPagesToTurn);
					var nextBtnLabel = activeTab == previewsTab ? paidNextBtnLabel : "Reroll2_previews_nextPage".Translate("");
					if (Widgets.ButtonText(new Rect(inRect.xMax - PageButtonSize.x, inRect.yMin, PageButtonSize.x, inRect.height), nextBtnLabel)) {
						PageForward(pageProvider, numPagesToTurn);
					}
				}
				DoMouseWheelPageTurning(pageProvider);
			}
		}

		private void OnPreviewFavoriteToggled(Widget_MapPreview preview) {
			var makeFavorite = !preview.IsFavorite;
			(makeFavorite ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
			if (makeFavorite) {
				favoritesProvider.Add(new Widget_MapPreview(preview));
			} else {
				favoritesProvider.Remove(preview);
			}
			var fav = favoritesProvider.TryFindPreview(preview.Seed);
			if (fav != null) fav.IsFavorite = makeFavorite;
			var gen = previewGenerator.TryFindPreview(preview.Seed);
			if (gen != null) gen.IsFavorite = makeFavorite;
		}

		private void DoMouseWheelPageTurning(BasePreviewPageProvider pageProvider) {
			if(Event.current.type != EventType.ScrollWheel) return;
			var scrollAmount = Event.current.delta.y;
			if (scrollAmount > 0) {
				// scroll within purchased pages unless shift is held
				if (pageProvider.CurrentPage < mapState.NumPreviewPagesPurchased - 1 || HugsLibUtility.ShiftIsHeld || !MapRerollController.Instance.PaidRerollsSetting) {
					PageForward(pageProvider);
				}
			} else if(scrollAmount < 0) {
				PageBackwards(pageProvider);
			}
		}

		public void PageForward(BasePreviewPageProvider pageProvider, int numPages = 1) {
			var pageToOpen = pageProvider.CurrentPage + numPages;
			if (activeTab == previewsTab && RerollToolbox.GetOperationCost(PaidOperationType.GeneratePreviews, pageToOpen) > 0) {
				RerollToolbox.ChargeForOperation(PaidOperationType.GeneratePreviews, pageToOpen);
			}
			pageProvider.OpenPage(pageToOpen);
		}

		public void PageBackwards(BasePreviewPageProvider pageProvider, int numPages = 1) {
			numPages = Mathf.Min(pageProvider.CurrentPage, numPages);
			pageProvider.OpenPage(pageProvider.CurrentPage - numPages);
		}

		private void OnTabSelected(int tabIndex) {
			activeTab = tabs[tabIndex];
		}
	}
}