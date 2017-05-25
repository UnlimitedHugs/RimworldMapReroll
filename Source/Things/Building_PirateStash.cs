using System;
using System.Collections.Generic;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MapReroll {
	[StaticConstructorOnStartup]
	public class Building_PirateStash : Building {
		private const int DropEveryTicks = 7;
		private const float DropLocationRadius = 10f;

		private static readonly Texture2D InscriptionGizmoTexture = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport");
		private static readonly Texture2D OpenGizmoTexture = ContentFinder<Texture2D>.Get("UI/Designators/Open");

		private ThingOwner<Thing> inventory;
		private int nextDropTick;
		private bool wantOpen;
		
		private bool justCreated;

		private BuildingProperties_PirateStash CustomProps {
			get { return def.building as BuildingProperties_PirateStash; }
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref nextDropTick, "nextDropTick", 0);
			Scribe_Values.Look(ref wantOpen, "wantOpen", false);
			Scribe_Deep.Look(ref inventory, "inventory", null);
		}

		public override void PostMake() {
			base.PostMake();
			justCreated = true;
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad) {
			base.SpawnSetup(map, respawningAfterLoad);
			if (justCreated) {
				AssignInventory();
			}
		}

		public override void Tick() {
			if(nextDropTick == 0 || inventory == null || nextDropTick > Find.TickManager.TicksGame || inventory.Count == 0) return;
			nextDropTick = Find.TickManager.TicksGame + DropEveryTicks;
			var dropIndex = Rand.Range(0, inventory.Count);
			var invStack = inventory[dropIndex];
			var dropLimit = invStack.stackCount;
			if (CustomProps != null) {
				for (int i = 0; i < CustomProps.dropStackLimits.Count; i++) {
					if (CustomProps.ResolvedContentDefs[i] != inventory[dropIndex].def) continue;
					dropLimit = Math.Max(1, CustomProps.dropStackLimits[i]);
				}
			}
			var placed = false;
			foreach (var pos in GenRadial.RadialCellsAround(Position, DropLocationRadius, false)) {
				if(!pos.InBounds(Map)) continue;
				var things = Map.thingGrid.ThingsListAtFast(pos);
				var validSpot = pos.Walkable(Map);
				for (int i = 0; i < things.Count; i++) {
					var t = things[i];
					// skip cells with items
					if (t.def == null || t.def.saveCompressible || t.def.passability != Traversability.Standable || t.def.category == ThingCategory.Item) {
						validSpot = false;
						break;
					}
				}
				if (!validSpot) continue;
				Thing placedThing;
				if (invStack.stackCount <= dropLimit) {
					inventory.TryDrop(invStack, pos, Map, ThingPlaceMode.Direct, out placedThing);
				} else {
					invStack.stackCount -= dropLimit;
					placedThing = ThingMaker.MakeThing(invStack.def);
					placedThing.stackCount = dropLimit;
					GenPlace.TryPlaceThing(placedThing, pos, Map, ThingPlaceMode.Direct);
				}
				placedThing.def.soundDrop.PlayOneShot(this);
				placed = true;
				break;
			}
			if (!placed) { // ran out of cells in radius
				inventory.TryDropAll(Position, Map, ThingPlaceMode.Near);
			}
			if (inventory.Count == 0) {
				nextDropTick = 0;
				inventory = null;
				MapRerollToolbox.TryReceiveSecretLetter(Position, Map);
			}
			
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish) {
			if ((mode == DestroyMode.KillFinalize || mode == DestroyMode.Deconstruct) && inventory != null) {
				inventory.TryDropAll(Position, Map, ThingPlaceMode.Near);
			}
			base.Destroy(mode);
		}

		public override IEnumerable<Gizmo> GetGizmos() {
			yield return new Command_Action {
				defaultLabel = "MapReroll_stash_readInscription".Translate(),
				icon = InscriptionGizmoTexture,
				action = () => Find.WindowStack.Add(new Dialog_MessageBox("MapReroll_stash_inscription".Translate())),
				hotKey = KeyBindingDefOf.Misc1
			};

			if (inventory != null && nextDropTick == 0) {
				yield return new Command_Toggle {
					defaultLabel = "DesignatorOpen".Translate(),
					defaultDesc = "DesignatorOpenDesc".Translate(),
					icon = OpenGizmoTexture,
					isActive = () => wantOpen,
					toggleAction = OnGizmoToggle,
					hotKey = KeyBindingDefOf.Misc2
				};
			}

			foreach (var gizmo in base.GetGizmos()) {
				yield return gizmo;
			}
		}

		private void OnGizmoToggle() {
			wantOpen = !wantOpen;
			this.ToggleDesignation(MapRerollDefOf.BreakOpenDesignation, wantOpen);
		}

		public bool WantsOpen() {
			return wantOpen;
		}

		public void Open() {
			if (CustomProps != null && CustomProps.openSoundDef != null) {
				CustomProps.openSoundDef.PlayOneShot(this);
			}
			const int dustPuffCount = 5;
			for (int i = 0; i < dustPuffCount; i++) {
				MoteMaker.ThrowDustPuff(Position, Map, Rand.Range(.5f, 2f));
			}
			nextDropTick = Find.TickManager.TicksGame + DropEveryTicks;
			wantOpen = false;
			this.ToggleDesignation(MapRerollDefOf.BreakOpenDesignation, wantOpen);
		}
		
		private void AssignInventory() {
			inventory = new ThingOwner<Thing>();
			var props = CustomProps;
			if(props == null) return;
			for (int i = 0; i < props.contentsDefs.Count; i++) {
				var itemDef = props.ResolvedContentDefs[i];
				var amount = props.contentsAmounts[i].RandomInRange;
				if (amount <= 0) continue;
				var thing = ThingMaker.MakeThing(itemDef);
				thing.stackCount = amount;
				inventory.TryAdd(thing);
			}
		}
	}
}