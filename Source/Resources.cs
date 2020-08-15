// ReSharper disable UnassignedField.Global
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace MapReroll {
	/// <summary>
	/// Auto-filled repository of all external resources referenced in the code
	/// </summary>
	public static class Resources {
		[DefOf]
		public static class Thing {
		}

		[DefOf]
		public static class Sound {
			public static SoundDef RerollSteamVent;
			public static SoundDef RerollDiceRoll;
		}
		
		[DefOf]
		public static class Settings {
			public static RerollSettingsDef MapRerollSettings;
		}

		[StaticConstructorOnStartup]
		public static class Textures {
			public static readonly Texture2D ResourceBarFull = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));
			public static readonly Texture2D ButtonAtlas = ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBG");
			public static Texture2D UIRerollMapOn;
			public static Texture2D UIRerollMapOff;
			public static Texture2D UIRerollGeysersOn;
			public static Texture2D UIRerollGeysersOff;
			public static Texture2D UIPreviewLoading;
			public static Texture2D UIDiceActive;
			public static Texture2D UIDiceInactive;
			public static Texture2D UISteelFront;
			public static Texture2D UISteelBack;
			public static Texture2D UIFavoriteStarOn;
			public static Texture2D UIFavoriteStarOff;
			public static Texture2D UICommitMapOn;
			public static Texture2D UICommitMapOff;

			static Textures() {
				foreach (var fieldInfo in typeof(Textures).GetFields(HugsLibUtility.AllBindingFlags)) {
					if(fieldInfo.IsInitOnly) continue;
					fieldInfo.SetValue(null, ContentFinder<Texture2D>.Get(fieldInfo.Name));
				}
				if (UISteelFront != null) {
					UISteelFront.wrapMode = TextureWrapMode.Repeat;
					UISteelFront.filterMode = FilterMode.Point;
				}
				if (UISteelBack != null) UISteelBack.wrapMode = TextureWrapMode.Repeat;
			}
		}
	}
}