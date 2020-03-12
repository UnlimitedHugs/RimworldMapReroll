using System;
using System.Reflection;
using HarmonyLib;
using HugsLib;
using Verse;

namespace MapReroll {
	/// <summary>
	/// Compatibility layer for EdB's Prepare Carefully mod.
	/// Fixes the randomly changing genders of animals when rerolling starting maps.
	/// Prevents Prepare Carefully from replacing its custom ScenParts with their vanilla equivalents,
	/// and adds proper gender saving to EdB.PrepareCarefully.ScenPart_CustomAnimal
	/// Side effects: removing Prepare Carefully after starting a new game will now cause errors, but these can be safely ignored.
	/// </summary>
	public static class Compat_PrepareCarefully {
		private const string PrepareCarefullyHarmonyId = "EdB.PrepareCarefully";

		private static FieldInfo CustomAnimalGenderField;

		public static void Apply(Harmony harmonyInst) {
			// delay until next frame to ensure mod load order independence (after patches are applied)
			HugsLibController.Instance.DoLater.DoNextUpdate(() => ApplyCompatibilityLayer(harmonyInst));
		}

		private static void ApplyCompatibilityLayer(Harmony harmonyInst) {
			try {
				if (!Harmony.HasAnyPatches(PrepareCarefullyHarmonyId)) return;
				
				const string customAnimalTypeName = "EdB.PrepareCarefully.ScenPart_CustomAnimal";
				var customAnimalType = GenTypes.GetTypeInAnyAssembly(customAnimalTypeName);
				if (customAnimalType == null) throw new NullReferenceException($"type not found: {customAnimalTypeName}");
				const string ExposeDataMethodName = "ExposeData";
				var customAnimalExposeMethod = customAnimalType.GetMethod(ExposeDataMethodName, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
				if(customAnimalExposeMethod == null || customAnimalExposeMethod.ReturnType != typeof(void) || customAnimalExposeMethod.GetParameters().Length != 0)
					throw new NullReferenceException($"method not found or mismatched signature: {customAnimalTypeName}.{ExposeDataMethodName}");
				const string GenderFieldName = "gender";
				CustomAnimalGenderField = customAnimalType.GetField(GenderFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
				if(CustomAnimalGenderField == null) 
					throw new NullReferenceException($"field not found: {customAnimalTypeName}.{GenderFieldName}");

				harmonyInst.Unpatch(typeof(Game).GetMethod("InitNewGame"), HarmonyPatchType.Postfix, PrepareCarefullyHarmonyId);

				harmonyInst.Patch(customAnimalExposeMethod, null, new HarmonyMethod(((Action<object>)CustomAnimalExposeDataPostfix).Method));
				MapRerollController.Instance.Logger.Message("Applied Prepare Carefully compatibility layer. Note: removing Prepare Carefully after starting a new game will now cause errors, but these can be safely ignored.");
			} catch (Exception e) {
				MapRerollController.Instance.Logger.Error("Failed to apply compatibility layer for Prepare carefully:" + e);
			}
		}

		private static void CustomAnimalExposeDataPostfix(object __instance) {
			if (CustomAnimalGenderField == null) return;
			var gender = (Gender)CustomAnimalGenderField.GetValue(__instance);
			Scribe_Values.Look(ref gender, "gender_mapReroll", Gender.Female, true);
			CustomAnimalGenderField.SetValue(__instance, gender);
		}
	}
}