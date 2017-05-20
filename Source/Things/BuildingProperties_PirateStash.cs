// ReSharper disable UnassignedField.Global
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MapReroll {
	public class BuildingProperties_PirateStash : BuildingProperties {
		public List<string> contentsDefs;
		public List<IntRange> contentsAmounts;
		public List<int> dropStackLimits;
		public int openWorkAmount;
		public SoundDef openSoundDef;
		public float commonality;
		public float mapSizeCommonalityBias;
		public List<ThingDef> ResolvedContentDefs { get; private set; }

		public virtual void PostLoad() {
			if (contentsDefs == null || contentsAmounts == null || dropStackLimits == null) {
				Log.Error("BuildingProperties_PirateStash requires fields: contentsDefs, contentsAmounts, dropStackLimits");
				return;
			}
			var defCount = contentsDefs.Count;
			ResolvedContentDefs = new List<ThingDef>(defCount);
			foreach (var defName in contentsDefs) {
				DirectXmlCrossRefLoader.RegisterListWantsCrossRef(ResolvedContentDefs, defName);
			}
			if (contentsAmounts.Count != defCount || dropStackLimits.Count != defCount)
				Log.Error("BuildingProperties_PirateStash: contentsDefs, contentsAmounts, dropStackLimits must contain the same number of items");
			if (commonality < 0) Log.Error("BuildingProperties_PirateStash: commonality cannot be less than than zero");
			if (mapSizeCommonalityBias < 0) Log.Error("BuildingProperties_PirateStash: mapSizeCommonalityBias cannot be less than than zero");
		}
	}
}