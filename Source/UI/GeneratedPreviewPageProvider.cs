using System;
using RimWorld.Planet;
using Verse;

namespace MapReroll.UI {
	public class GeneratedPreviewPageProvider : BasePreviewPageProvider {
		private readonly Map startingMap;
		private readonly World world;
		private MapPreviewGenerator previewGenerator;
		private string lastGeneratedSeed;
		private int numQueuedPreviews;

		public GeneratedPreviewPageProvider(Map currentMap, World world) {
			startingMap = currentMap;
			this.world = world;
			var mapState = RerollToolbox.GetStateForMap(currentMap);
			lastGeneratedSeed = RerollToolbox.CurrentMapSeed(mapState);
			previewGenerator = new MapPreviewGenerator();
		}
		
		public Action<Widget_MapPreview> OnFavoriteToggled { get; set; }

		public int NumQueuedPreviews {
			get { return numQueuedPreviews; }
		}

		public override void OpenPage(int pageIndex) {
			EnsureEnoughPreviewsForPage(pageIndex);
			base.OpenPage(pageIndex);
		}

		public override void Dispose() {
			base.Dispose();
			previewGenerator.Dispose();
		}

		public void WaitForDisposal() {
			previewGenerator.WaitForDisposal();
		}

		public override bool PageIsAvailable(int pageIndex) {
			return pageIndex >= 0;
		}

		private void EnsureEnoughPreviewsForPage(int page) {
			while (previews.Count <= MaxIndexOnPage(page)) {
				if (previews.Count == 0) {
                    Compat.Compat_ConfigurableMaps.UpdateConfigs();
				}
				previews.Add(CreatePreview());
			}
		}

		private Widget_MapPreview CreatePreview() {
			lastGeneratedSeed = RerollToolbox.GetNextRerollSeed(lastGeneratedSeed);
			var promise = previewGenerator.QueuePreviewForSeed(lastGeneratedSeed, startingMap.Tile, world.info.initialMapSize.x, MapRerollController.Instance.PreviewCavesSetting);
			numQueuedPreviews++;
			promise.Finally(() => numQueuedPreviews--);
			return new Widget_MapPreview(promise, lastGeneratedSeed) {OnFavoriteToggled = OnFavorite};
		}

		private void OnFavorite(Widget_MapPreview widget) {
			if (OnFavoriteToggled != null) OnFavoriteToggled(widget);
		}
	}
}