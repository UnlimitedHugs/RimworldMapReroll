using System;
using System.Collections.Generic;
using MapReroll.Interpolation;
using UnityEngine;

namespace MapReroll.UI {
	public abstract class BasePreviewPageProvider : IDisposable {
		public const int PreviewsPerPage = 9;
		private const float PreviewSpacing = 10;
		private const float PageFlipDuration = .5f;

		protected List<Widget_MapPreview> previews = new List<Widget_MapPreview>();
		protected int currentPage;
		protected Widget_MapPreview overlayPreview;
		
		private ValueInterpolator pageInterpolator;
		private int outgoingPage = -1;

		public virtual int CurrentPage {
			get { return currentPage; }
		}

		public int NumPagesAvailable {
			get { return Mathf.CeilToInt(previews.Count / (float)PreviewsPerPage); }
		}

		public int PreviewCount {
			get { return previews.Count; }
		}

		public Widget_MapPreview CurrentZoomedInPreview {
			get { return overlayPreview != null && overlayPreview.IsFullyZoomedIn ? overlayPreview : null; }
		}

		protected BasePreviewPageProvider() {
			pageInterpolator = new ValueInterpolator(1f);
		}

		public IEnumerable<Widget_MapPreview> AllPreviews {
			get { return previews; }
		}

		public virtual void OpenPage(int pageIndex) {
			if(!PageIsAvailable(pageIndex)) return;
			overlayPreview = null;
			if (pageIndex != currentPage) {
				outgoingPage = currentPage;
				currentPage = pageIndex;
				pageInterpolator.value = 0f;
				pageInterpolator.StartInterpolation(1f, PageFlipDuration, CurveType.CubicInOut).SetFinishedCallback(OnPageFlipFinished);
			}
		}

		public virtual void Dispose() {
			foreach (var preview in previews) {
				preview.Dispose();
			}
		}

		public virtual bool PageIsAvailable(int pageIndex) {
			return pageIndex >= 0 && MaxIndexOnPage(pageIndex-1)+1 < previews.Count;
		}

		public virtual void Draw(Rect inRect) {
			if (Event.current.type == EventType.Repaint) {
				pageInterpolator.Update();
			}
			var offscreenPageOffset = inRect.width + PreviewSpacing * 2f;
			var interpolatedOffset = pageInterpolator.value * offscreenPageOffset;
			var backFlip = outgoingPage > currentPage;
			var outgoingOffset = backFlip ? interpolatedOffset : -interpolatedOffset;
			var currentOffset = backFlip ? interpolatedOffset - offscreenPageOffset : offscreenPageOffset - interpolatedOffset;
			if (PageTransitionInProgress) {
				var outgoingRect = new Rect(inRect.x + outgoingOffset, inRect.y, inRect.width, inRect.height);
				DrawPage(outgoingPage, outgoingRect);
			}
			
			var currentPageRect = new Rect(inRect.x + currentOffset, inRect.y, inRect.width, inRect.height);
			DrawPage(currentPage, currentPageRect);
			
			if (overlayPreview != null) {
				overlayPreview.DrawOverlay(inRect);
			}
		}

		private void DrawPage(int page, Rect inRect) {
			float rowCount = Mathf.Sqrt(PreviewsPerPage);
			var totalSpacing = PreviewSpacing * (rowCount - 1);
			var previewSize = new Vector2((inRect.width - totalSpacing) / rowCount, (inRect.height - totalSpacing) / rowCount);
			bool anyOverlayDrawingRequired = false;
			var maxPreviewIndex = Mathf.Min(MaxIndexOnPage(page), previews.Count-1);
			for (int i = MinIndexOnPage(page); i <= maxPreviewIndex; i++) {
				var preview = previews[i];
				var previewPosition = GetPreviewPositionFromIndex(i);
				var previewRect = new Rect(
					inRect.x + (inRect.width - previewSize.x) * previewPosition.x,
					inRect.y + (inRect.height - previewSize.y) * previewPosition.y,
					previewSize.x, previewSize.y);
				var isInteractive = overlayPreview == null && !PageTransitionInProgress;
				preview.Draw(previewRect, i, isInteractive);
				if (preview.WantsOverlayDrawing) {
					overlayPreview = preview;
					anyOverlayDrawingRequired = true;
				}
			}
			if (!anyOverlayDrawingRequired) {
				overlayPreview = null;
			}
		}

		private void OnPageFlipFinished(ValueInterpolator interpolator, float finalValue, float interpolationDuration, InterpolationCurves.Curve interpolationCurve) {
			interpolator.value = finalValue;
			outgoingPage = -1;
		}

		private bool PageTransitionInProgress {
			get { return outgoingPage >= 0; }
		}

		protected int MinIndexOnPage(int page) {
			return page * PreviewsPerPage;
		}

		protected int MaxIndexOnPage(int page) {
			return page * PreviewsPerPage + PreviewsPerPage - 1;
		}

		private Vector2 GetPreviewPositionFromIndex(int previewIndex) {
			var indexOnPage = previewIndex % PreviewsPerPage;
			float rowCount = Mathf.Sqrt(PreviewsPerPage);
			var indexInRow = indexOnPage % rowCount;
			var indexInCol = Mathf.Floor(indexOnPage / rowCount);
			return new Vector2(indexInRow / (rowCount - 1f), indexInCol / (rowCount - 1f));
		}
	}
}