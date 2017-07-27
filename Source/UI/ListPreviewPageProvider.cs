using System.Collections;
using System.Collections.Generic;

namespace MapReroll.UI {
	public class ListPreviewPageProvider : BasePreviewPageProvider, IList<Widget_MapPreview> {
		public IEnumerator<Widget_MapPreview> GetEnumerator() {
			return previews.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public void Add(Widget_MapPreview item) {
			previews.Add(item);
		}

		public void Clear() {
			previews.Clear();
			overlayPreview = null;
			currentPage = 0;
		}

		public bool Contains(Widget_MapPreview item) {
			return previews.Contains(item);
		}

		public void CopyTo(Widget_MapPreview[] array, int arrayIndex) {
			previews.CopyTo(array, arrayIndex);
		}

		public bool Remove(Widget_MapPreview item) {
			return previews.Remove(item);
		}

		public int Count {
			get { return PreviewCount; }
		}

		public bool IsReadOnly {
			get { return false; }
		}
		public int IndexOf(Widget_MapPreview item) {
			return previews.IndexOf(item);
		}

		public void Insert(int index, Widget_MapPreview item) {
			previews.Insert(index, item);
		}

		public void RemoveAt(int index) {
			previews.RemoveAt(index);
		}

		public Widget_MapPreview this[int index] {
			get { return previews[index]; }
			set { previews[index] = value; }
		}
	}
}