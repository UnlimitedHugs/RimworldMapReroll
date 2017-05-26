using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;

namespace MapReroll {
	public static class ReflectionCache {
		public static Type ScenPartCreateIncidentType { get; private set; }

		public static FieldInfo Thing_State { get; private set; }
		public static FieldInfo GenstepScatterer_UsedSpots { get; private set; }
		public static FieldInfo PawnArriveMethod_Method { get; private set; }
		public static FieldInfo Scenario_Parts { get; private set; }
		public static FieldInfo CreateIncident_IsFinished { get; private set; }
		public static FieldInfo Building_SustainerAmbient { get; private set; }
		public static FieldInfo MapParent_AnyCaravanEverFormed { get; private set; }

		public static void PrepareReflection() {
			ScenPartCreateIncidentType = ReflectType("RimWorld.ScenPart_CreateIncident", typeof(ScenPart).Assembly);

			Thing_State = ReflectField("mapIndexOrState", typeof(Thing), typeof(sbyte));
			GenstepScatterer_UsedSpots = ReflectField("usedSpots", typeof(GenStep_Scatterer), typeof(List<IntVec3>));
			PawnArriveMethod_Method = ReflectField("method", typeof(ScenPart_PlayerPawnsArriveMethod), typeof(PlayerPawnsArriveMethod));
			Scenario_Parts = ReflectField("parts", typeof(Scenario), typeof(List<ScenPart>));
			Building_SustainerAmbient = ReflectField("sustainerAmbient", typeof(Building), typeof(Sustainer));
			MapParent_AnyCaravanEverFormed = ReflectField("anyCaravanEverFormed", typeof(MapParent), typeof(bool));
			if (ScenPartCreateIncidentType != null) {
				CreateIncident_IsFinished = ReflectField("isFinished", ScenPartCreateIncidentType, typeof(bool));
			}
		}

		private static Type ReflectType(string nameWithNamespace, Assembly assembly = null) {
			Type type;
			if (assembly == null) {
				type = GenTypes.GetTypeInAnyAssembly(nameWithNamespace);
			} else {
				type = assembly.GetType(nameWithNamespace, false, false);
			}
			if (type == null) {
				MapRerollController.Instance.Logger.Error("Failed to reflect required type \"{0}\"", nameWithNamespace);
			}
			return type;
		}

		private static FieldInfo ReflectField(string name, Type parentType, Type expectedFieldType) {
			var field = AccessTools.Field(parentType, name);
			if (field == null) {
				MapRerollController.Instance.Logger.Error("Failed to reflect required field \"{0}\" in type \"{1}\".", name, parentType);
			} else if (expectedFieldType != null && field.FieldType != expectedFieldType) {
				MapRerollController.Instance.Logger.Error("Reflect field \"{0}\" did not match expected field type of \"{1}\".", name, expectedFieldType);
			}
			return field;
		}
	}
}