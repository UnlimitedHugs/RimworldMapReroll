using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MapReroll {
	public class WorkGiver_PirateStash : WorkGiver_Scanner {
		
		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn Pawn) {
			var stashes = Find.DesignationManager.DesignationsOfDef(MapRerollDefOf.BreakOpenDesignation);
			foreach (var stash in stashes) {
				yield return stash.target.Thing;
			}
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t) {
			if (!(t is Building_PirateStash)) return false;
			return
				!pawn.Dead
				&& !pawn.Downed
				&& !pawn.IsBurning()
				&& (t as Building_PirateStash).WantsOpen()
				&& pawn.CanReserveAndReach(t, PathEndMode.Touch, Danger.Deadly);
		}

		public override Job JobOnThing(Pawn pawn, Thing t) {
			return new Job(MapRerollDefOf.JobDef_OpenStash, t);
		}
	}
}