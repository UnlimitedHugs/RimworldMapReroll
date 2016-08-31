using UnityEngine;
using Verse;

namespace MapReroll {
	public class MapRerollDef : Def {

		public float geyserRerollCost;
		public float mapRerollCost;
		public bool enableInterface;
		public Vector2 interfaceOffset;
		public bool logConsumedResources;
		public float diceWidgetSize;
		public bool useSillyLoadingMessages;
		public int numLoadingMessages;
	}
}