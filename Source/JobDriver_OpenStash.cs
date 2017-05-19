using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MapReroll {
	public class JobDriver_OpenStash : JobDriver {
		private float workLeft;

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref workLeft, "workLeft", 0f);
		}

		protected override IEnumerable<Toil> MakeNewToils() {
			AddFailCondition(JobHasFailed);
			var stash = TargetThingA as Building_PirateStash;
			if(stash == null) yield break;
			yield return Toils_Reserve.Reserve(TargetIndex.A);
			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
			var toil = new Toil {
				initAction = delegate {
					workLeft = TotalWorkAmount;
				},
				tickAction = delegate {
					var statValue = GetActor().GetStatValue(StatDefOf.ConstructionSpeed);
					workLeft -= statValue;
					if (workLeft > 0) return;
					if(stash.WantsOpen()) stash.Open();
					ReadyForNextToil();
				}
			};
			toil.WithEffect(EffecterDefOf.ConstructMetal, TargetIndex.A);
			toil.WithProgressBar(TargetIndex.A, () => 1f - workLeft/TotalWorkAmount);
			toil.defaultCompleteMode = ToilCompleteMode.Never;
			yield return toil;
			yield return Toils_Reserve.Release(TargetIndex.A);
		}

		private int TotalWorkAmount {
			get {
				var props = MapRerollDefOf.PirateStash.building as BuildingProperties_PirateStash;
				return props != null ? props.openWorkAmount : 0;
			}
		}

		private bool JobHasFailed() {
			var stash = TargetThingA as Building_PirateStash;
			return TargetThingA.Destroyed || stash == null || !stash.WantsOpen();
		}
	}
}