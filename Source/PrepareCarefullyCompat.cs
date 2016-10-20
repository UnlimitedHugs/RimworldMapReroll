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
	 * Another issue is that our GameInitData.startingPawns interception happens *before* PrepareCarefully fills in its custom colonists,
	 * so we need to pull the correct colonists from the controller.
	 */
	public class PrepareCarefullyCompat {
		private const string PrepareCarefullyTypeName = "EdB.PrepareCarefully.PrepareCarefully";
		private const string InstanceFieldName = "instance";
		private const string ColonistsFieldName = "colonists";
		private const string ActiveFieldName = "active";

		private static PrepareCarefullyCompat instance;
		public static PrepareCarefullyCompat Instance {
			get { return instance ?? (instance = new PrepareCarefullyCompat()); }
		}

		private bool reflectionPerformed;
		private Type controllerType;
		private FieldInfo controllerColonistsField;
		private FieldInfo controllerActiveField;
		private object controllerInstance;


		private PrepareCarefullyCompat() {
		}

		public List<Pawn> TryGetCustomColonists() {
			if (!reflectionPerformed) ReflectRequiredFields();
			if (!ControllerIsActive()) return null;
			return (List<Pawn>) controllerColonistsField.GetValue(controllerInstance);
		} 

		public void UpdateCustomColonists(IEnumerable<Pawn> colonists) {
			if (!reflectionPerformed) ReflectRequiredFields();
			if (!ControllerIsActive()) return;
			controllerColonistsField.SetValue(controllerInstance, colonists.ToList()); // you can have a copy, thank you very much- not making that mistake again :)
		}

		private void ReflectRequiredFields() {
			reflectionPerformed = true;
			try {
				controllerType = GenTypes.GetTypeInAnyAssembly(PrepareCarefullyTypeName);
				if (controllerType == null) return;
				controllerActiveField = GetPCField(controllerType, ActiveFieldName, BindingFlags.NonPublic | BindingFlags.Instance, typeof (bool));
				var instanceField = GetPCField(controllerType, InstanceFieldName, BindingFlags.NonPublic | BindingFlags.Static, controllerType);
				controllerInstance = instanceField.GetValue(null);
				if (controllerInstance == null) throw new Exception("controller instance is null");
				controllerColonistsField = GetPCField(controllerType, ColonistsFieldName, BindingFlags.NonPublic | BindingFlags.Instance, typeof (List<Pawn>));
			} catch (Exception e) {
				MapRerollController.Instance.Logger.Warning("PrepareCarefully compat failure: "+e);
				controllerType = null;
			}
		}

		private bool ControllerIsActive() {
			return controllerType != null && (bool) controllerActiveField.GetValue(controllerInstance);
		}

		private FieldInfo GetPCField(Type pcType, string fieldName, BindingFlags flags, Type expectedType) {
			var field = pcType.GetField(fieldName, flags);
			if (field == null) throw new Exception("could not reflect field: "+fieldName);
			if (field.FieldType != expectedType) throw new Exception("field did not match expected type: " + fieldName);
			return field;
		}
	}

}