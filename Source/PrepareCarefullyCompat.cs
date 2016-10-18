using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace MapReroll {
	/**
	 * A compatibility fix for PrepareCarefully.
	 * Since we are now generating new pawn instances on reroll, and PC keeps an internal list of spawned pawns, 
	 * that list needs updating- otherwise we'll see discarded pawns being spawned back in.
	 */
	public static class PrepareCarefullyCompat {
		private const string PrepareCarefullyTypeName = "EdB.PrepareCarefully.PrepareCarefully";
		private const string InstanceFieldName = "instance";
		private const string ColonistsFieldName = "colonists";

		public static void UpdateCustomColonists(IEnumerable<Pawn> colonists) {
			var pcType = GenTypes.GetTypeInAnyAssembly(PrepareCarefullyTypeName);
			if(pcType == null) return;
			var instanceField = GetPCField(pcType, InstanceFieldName, BindingFlags.NonPublic | BindingFlags.Static, pcType);
			if(instanceField == null) return;
			var inst = instanceField.GetValue(null);
			if(inst == null) return;
			var colonistsField = GetPCField(pcType, ColonistsFieldName, BindingFlags.NonPublic | BindingFlags.Instance, typeof(List<Pawn>));
			if(colonistsField == null) return;
			colonistsField.SetValue(inst, colonists.ToList()); // you can have a copy, thank you very much- not making that mistake again :)
		}

		private static FieldInfo GetPCField(Type pcType, string fieldName, BindingFlags flags, Type expectedType) {
			var field = pcType.GetField(fieldName, flags);
			if (field == null) {
				MapRerollController.Instance.Logger.Warning("PrepareCarefully compat failure: could not reflect field: "+fieldName);
				return null;
			}
			if (field.FieldType != expectedType) {
				MapRerollController.Instance.Logger.Warning("PrepareCarefully compat failure: field did not match expected type: " + fieldName);
				return null;
			}
			return field;
		}
	}

}