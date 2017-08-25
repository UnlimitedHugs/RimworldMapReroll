using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MapReroll {
	public class GeyserRerollTool {
		public const float ArrowPointingDuration = 10f;
		public const int SteamEffectDurationTicks = GenTicks.TicksPerRealSecond * 5;
		public const bool AllowGeyserCountReduction = true;

		private readonly List<TimedGeyserArrow> drawnArrows = new List<TimedGeyserArrow>();
		private readonly List<TimedSteamEffect> activeSteamEffects = new List<TimedSteamEffect>();
		
		public bool RerollInProgress { get; private set; }
		
		public void OnUpdate() {
			if (Current.ProgramState != ProgramState.Playing) return;
			if (drawnArrows.Count > 0) {
				DrawGeyserArrows(drawnArrows);
			}
		}

		public void OnTick() {
			if (activeSteamEffects.Count > 0) {
				DrawSteamEffects(activeSteamEffects);
			}
		}

		public void DoReroll() {
			var map = Find.VisibleMap;
			var logger = MapRerollController.Instance.Logger;
			if (RerollInProgress) {
				logger.Error("Cannot reroll geysers- reroll already in progress");
				return;
			}
			if (map == null) {
				logger.Error("No visible map- cannot reroll geysers");
				return;
			}
			var state = RerollToolbox.GetStateForMap(map);
			if (state.UsedMapGenerator == null) {
				logger.Error(string.Format("Cannot reroll geysers: map {0} does not have a recorded MapGeneratorDef", map));
				return;
			}
			var geyserDef = ThingDefOf.SteamGeyser;
			var genStepDef = state.UsedMapGenerator.GenSteps.FirstOrDefault(g => g.genStep is GenStep_ScatterThings
																			&& (g.genStep as GenStep_ScatterThings).thingDef == geyserDef);
			if (genStepDef == null) {
				logger.Error(string.Format("Cannot reroll geysers: map generator {0} does not have a geyser GenStep", state.UsedMapGenerator));
				return;
			}
			drawnArrows.Clear();
			activeSteamEffects.Clear();
			var geysersOnMap = map.listerThings.AllThings.Where(t => t.def == geyserDef);
			var oldGeysers = new HashSet<Thing>(geysersOnMap);
			genStepDef.genStep.Generate(map);
			var newGeysers = map.listerThings.AllThings.Where(t => t.def == geyserDef).Except(oldGeysers);
			BeginGeyserSpawning(oldGeysers, newGeysers, map);
		}

		private void BeginGeyserSpawning(IEnumerable<Thing> oldGeysers, IEnumerable<Thing> newGeysers, Map map) {
			var oldGeysersQueue = new Queue<Thing>(oldGeysers.InRandomOrder());
			var newGeysersList = newGeysers.ToList();
			var newGeysersQueue = new Queue<Thing>(newGeysersList);
			foreach (var thing in newGeysersList) {
				thing.DeSpawn();
			}
			var allowReduction = AllowGeyserCountReduction && oldGeysersQueue.Count > 1; // make sure we have at least one geyser on the map, even if we don't spawn new ones
			var numSpawnOperations = Math.Max(oldGeysersQueue.Count, newGeysersQueue.Count);
			if (numSpawnOperations > 0) {
				RerollInProgress = true;
			}
			for (int i = 1; i <= numSpawnOperations; i++) {
				var operationIndex = i;
				var newGeyser = newGeysersQueue.Count > 0 ? newGeysersQueue.Dequeue() : null;
				if (newGeyser != null) {
					GenSpawn.Spawn(newGeyser, newGeyser.Position, map);
					AddArrowDrawerFor(newGeyser);
					AddSteamEffectFor(newGeyser);
					PlaySteamVentSound(newGeyser);
				}
				var oldGeyser = oldGeysersQueue.Count > 0 ? oldGeysersQueue.Dequeue() : null;
				if (newGeyser != null || allowReduction) {
					if (oldGeyser != null) {
						Thing.allowDestroyNonDestroyable = true;
						oldGeyser.Destroy();
						Thing.allowDestroyNonDestroyable = false;
					}
				} else {
					AddArrowDrawerFor(oldGeyser);
				}
				if (operationIndex >= numSpawnOperations) {
					RerollInProgress = false;
				}
			}
		}

		private void PlaySteamVentSound(Thing vent) {
			Resources.Sound.RerollSteamVent.PlayOneShot(vent);
		}

		private void AddArrowDrawerFor(Thing t) {
			if (MapRerollController.Instance.GeyserArrowsSetting) {
				drawnArrows.Add(new TimedGeyserArrow(t.TrueCenter(), Time.unscaledTime + ArrowPointingDuration));
			}
		}

		private void AddSteamEffectFor(Thing t) {
			activeSteamEffects.Add(new TimedSteamEffect(t, Find.TickManager.TicksGame + SteamEffectDurationTicks));
		}

		private void DrawGeyserArrows(List<TimedGeyserArrow> arrows) {
			// do not draw on world map
			if (Find.World == null || Find.VisibleMap == null || Find.World.renderer == null || Find.World.renderer.wantedMode != WorldRenderMode.None) return;
			for (int i = 0; i < arrows.Count; i++) {
				var arrow = arrows[i];
				GenDraw.DrawArrowPointingAt(arrow.ArrowTarget);
			}
			arrows.RemoveAll(a => Time.unscaledTime > a.ExpireTime);
		}

		private void DrawSteamEffects(List<TimedSteamEffect> effects) {
			if (Find.World == null || Find.VisibleMap == null) return;
			for (int i = 0; i < effects.Count; i++) {
				var effect = effects[i];
				MoteMaker.ThrowAirPuffUp(effect.Geyser.TrueCenter(), effect.Geyser.Map);
			}
			effects.RemoveAll(a => Find.TickManager.TicksGame > a.ExpireTick);
		}

		private class TimedGeyserArrow {
			public readonly Vector3 ArrowTarget;
			public readonly float ExpireTime;
			public TimedGeyserArrow(Vector3 arrowTarget, float expireTime) {
				ArrowTarget = arrowTarget;
				ExpireTime = expireTime;
			}
		}

		private class TimedSteamEffect {
			public readonly Thing Geyser;
			public readonly int ExpireTick;
			public TimedSteamEffect(Thing geyser, int expireTick) {
				Geyser = geyser;
				ExpireTick = expireTick;
			}
		}
	}
}