using System.Collections.Generic;
using Verse;

namespace MapReroll {
	/// <summary>
	/// Describes a possible map size for map generation
	/// </summary>
	public struct MapSize {
		private static List<MapSize> _sizes;

		public static List<MapSize> AvailableMapSizes {
			get {
				return _sizes ?? (_sizes = new List<MapSize> {
					new MapSize(75) {Hidden = true},
					new MapSize(200) {Label = "MapSizeSmall".Translate()},
					new MapSize(225),
					new MapSize(250) {Label = "MapSizeMedium".Translate(), IsDefault = true},
					new MapSize(275),
					new MapSize(300) {Label = "MapSizeLarge".Translate()},
					new MapSize(325),
					new MapSize(350) {Label = "MapSizeExtreme".Translate()},
					new MapSize(400)
				});
			}
		}

		public static bool TryResolve(int size, out MapSize resolvedSize) {
			var sizes = AvailableMapSizes;
			for (int i = 0; i < sizes.Count; i++) {
				if (sizes[i].Size == size) {
					resolvedSize = sizes[i];
					return true;
				}
			}
			resolvedSize = DefaultSize;
			return false;
		}

		public static MapSize DefaultSize {
			get {
				var sizes = AvailableMapSizes;
				for (int i = 0; i < sizes.Count; i++) {
					if (sizes[i].IsDefault) return sizes[i];
				}
				return sizes[0];
			}
		}

		public readonly int Size;
		public string Label { get; set; }
		public bool IsDefault { get; set; }
		public bool Hidden { get; set; }

		public MapSize(int size) {
			Size = size;
			Label = null;
			IsDefault = false;
			Hidden = false;
		}

		public override string ToString() {
			return $"{Size}x{Size}{(Label != null ? " - " + Label : string.Empty)}";
		}
	}
}