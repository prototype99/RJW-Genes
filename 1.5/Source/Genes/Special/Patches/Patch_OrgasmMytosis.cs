using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.QuestGen;
using rjw;
using rjw.Modules.Shared.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;


namespace RJW_Genes
{

    /// <summary>
    /// There was a big change with RJW 5.3.6 and I got a new Issue #52 documenting it. 
    /// Basically, the reroll and orgasm logic was changed. 
    /// </summary>
	
	[HarmonyPatch(typeof(JobDriver_Sex), "SetupOrgasmTicks")]
	public static class Patch_OrgasmMytosis
	{

		private const float SEVERITY_INCREASE_PER_ORGASM = 0.075f;

        public static void Postfix(JobDriver_Sex __instance)
		{
			Pawn orgasmingPawn = __instance.pawn;
            bool hasPollutedMytosis = false;

            if (orgasmingPawn == null || orgasmingPawn.genes == null) { return; }

            if ((GeneUtility.HasGeneNullCheck(orgasmingPawn, GeneDefOf.rjw_genes_sexual_mytosis) || hasPollutedMytosis) && ! orgasmingPawn.health.hediffSet.HasHediff(HediffDefOf.rjw_genes_mytosis_shock_hediff))
			{
				var mytosisHediff = GetOrgasmMytosisHediff(orgasmingPawn);
				mytosisHediff.Severity += SEVERITY_INCREASE_PER_ORGASM;
                if(hasPollutedMytosis && orgasmingPawn.Spawned && GridsUtility.IsPolluted(orgasmingPawn.Position, orgasmingPawn.Map))
                {
                    mytosisHediff.Severity -= SEVERITY_INCREASE_PER_ORGASM;
                }

				if (mytosisHediff.Severity >= 1.0)
                {
                    orgasmingPawn.health.RemoveHediff(mytosisHediff);

                    var copy = Multiply(orgasmingPawn);

                    ApplyMytosisShock(copy);
                    ApplyMytosisShock(orgasmingPawn);

                    orgasmingPawn.Strip();
                    
                }
                else
				{
                    float orgasm_time_reduction = Math.Max(1.0f - mytosisHediff.Severity, 0.1f);
                    __instance.sex_ticks = (int) (__instance.sex_ticks * orgasm_time_reduction);
                }

			}

		}

        private static void ApplyMytosisShock(Pawn copy)
        {
            var stunA = HediffMaker.MakeHediff(HediffDefOf.rjw_genes_mytosis_shock_hediff, copy);
            stunA.Severity = 1;
            copy.health.AddHediff(stunA);
        }

        /// <summary>
        /// Helps to get the Orgasm Mytosis Hediff of a Pawn. If it does not exist, one is added. 
        /// </summary>
        /// <param name="orgasmed">The pawn that had the orgasm, for which a hediff is looked up or created.</param>
        /// <returns></returns>
        public static Hediff GetOrgasmMytosisHediff(Pawn orgasmed)
		{
			Hediff orgasmicMytosisHediff = orgasmed.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.rjw_genes_orgasmic_mytosis_hediff);
			if (orgasmicMytosisHediff == null)
			{
				orgasmicMytosisHediff = HediffMaker.MakeHediff(HediffDefOf.rjw_genes_orgasmic_mytosis_hediff, orgasmed);
				orgasmicMytosisHediff.Severity = 0;
				orgasmed.health.AddHediff(orgasmicMytosisHediff);
			}
			return orgasmicMytosisHediff;
		}

		public static Pawn Multiply(Pawn toMultiply)
		{
			if (RJW_Genes_Settings.rjw_genes_detailed_debug) ModLog.Message("Hitting Multiply of Mytosis Pawn!");

            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: toMultiply.kindDef,
                faction: toMultiply.Faction,
                forceGenerateNewPawn: true,
                developmentalStages: DevelopmentalStage.Adult,
                allowDowned: true,
                canGeneratePawnRelations: false,
                colonistRelationChanceFactor: 0,
                allowFood: false,
                allowAddictions: false,
                relationWithExtraPawnChanceFactor: 0,
                forbidAnyTitle: true,
                forceNoBackstory: true,
                fixedGender: toMultiply.gender
                );

            /*
             * Devnote: Adding these will lead to deadly issues! 
				fixedBiologicalAge: toMultiply.ageTracker.AgeBiologicalTicks,
				fixedChronologicalAge: toMultiply.ageTracker.AgeChronologicalTicks,
            */

            Pawn copy = PawnGenerator.GeneratePawn(request);
            

            copy.gender = toMultiply.gender;
            // FIXED: Copy age values instead of sharing ageTracker reference
            copy.ageTracker.AgeBiologicalTicks = toMultiply.ageTracker.AgeBiologicalTicks;
            copy.ageTracker.AgeChronologicalTicks = toMultiply.ageTracker.AgeChronologicalTicks;
            copy.ageTracker.BirthAbsTicks = toMultiply.ageTracker.BirthAbsTicks;
            copy.Name = CreateCloneName(toMultiply,2);

            copy.health = CopyRelevantHediffs(copy, toMultiply);
            copy.genes = CopyGeneTracker(copy,toMultiply.genes);

            // FIXED: Create new ideo reference instead of sharing
            copy.ideo = new Pawn_IdeoTracker(copy);
            if (toMultiply.ideo?.Ideo != null)
            {
                copy.ideo.SetIdeo(toMultiply.ideo.Ideo);
            }
            copy.records = new Pawn_RecordsTracker(copy);

            // FIXED: Create new relations tracker instead of sharing reference
            copy.relations = new Pawn_RelationsTracker(copy);
            
            copy.skills = CopySkillTracker(copy,toMultiply.skills);

            copy.equipment.DestroyAllEquipment();
            copy.apparel.DestroyAll();


            PawnUtility.TrySpawnHatchedOrBornPawn(copy, toMultiply);
            // Move the copy in front of the origin, rather than on top
            if (toMultiply.Spawned)
                if (toMultiply.CurrentBed() != null)
                {
                    copy.Position = copy.Position + new IntVec3(0, 0, 1).RotatedBy(toMultiply.CurrentBed().Rotation);
                }


            // Establish parent-child relationship between original and clone
            // Note: This may still log a warning if trying to relate to self, but won't crash
            try 
            {
                copy.relations.AddDirectRelation(PawnRelationDefOf.Parent, toMultiply);
            }
            catch (System.Exception ex)
            {
                if (RJW_Genes_Settings.rjw_genes_detailed_debug) 
                    ModLog.Warning($"Could not establish parent relation: {ex.Message}");
            }

            copy.style = CopyStyleTracker(copy, toMultiply.style);
            copy.story = CopyStoryTracker(copy, toMultiply.story);

            copy.genes.xenotypeName = toMultiply.genes.xenotypeName;
            copy.story.favoriteColor = toMultiply.story.favoriteColor;

            Find.LetterStack.ReceiveLetter("Orgasmic Mytosis", $"{toMultiply.NameShortColored} performed mytosis on orgasm! The pawn and its clone entered a regenerative state.",
                RimWorld.LetterDefOf.NeutralEvent, copy);

            return copy;
		}

        private static Name CreateCloneName(Pawn toCopyFrom, int additions=1)
        {
            if (toCopyFrom.Name is NameTriple)
            {
                NameTriple casted = (NameTriple)toCopyFrom.Name;
                String Postfix = " " + RandomNamePostFix(additions);
                Name newName = new NameTriple(first:casted.First + Postfix, nick: casted.Nick + Postfix, last: casted.Last);
                if (newName.UsedThisGame)
                    return CreateCloneName(toCopyFrom, additions);
                return newName;
            }
            return toCopyFrom.Name;
        }

        private static Pawn_GeneTracker CopyGeneTracker(Pawn toCopyTo, Pawn_GeneTracker toCopyFrom)
        {
            var tracker = new Pawn_GeneTracker(toCopyTo);
            // Due to Overwrite logics, we first add Endogenes and then a second pass on xenogenes

            // Pass 1: Endogenes
            foreach (Gene gene in toCopyFrom.GenesListForReading) {
                GeneDef def = gene.def;
                if (!toCopyFrom.Xenogenes.Contains(gene))
                    tracker.AddGene(def, false);
            }

            // Pass 2: Xenogenes
            foreach (Gene gene in toCopyFrom.GenesListForReading)
            {
                GeneDef def = gene.def;
                if (toCopyFrom.Xenogenes.Contains(gene))
                    tracker.AddGene(def, true);
            }

            tracker.Reset();
            return tracker;
        }

        private static Pawn_StoryTracker CopyStoryTracker(Pawn toCopyTo, Pawn_StoryTracker toCopyFrom)
        {
            var tracker = new Pawn_StoryTracker(toCopyTo);

            tracker.Childhood = toCopyFrom.Childhood;
            tracker.Adulthood = toCopyFrom.Adulthood;

            tracker.headType = toCopyFrom.headType;
            tracker.bodyType = toCopyFrom.bodyType;
            tracker.hairDef = toCopyFrom.hairDef;
            tracker.furDef = toCopyFrom.furDef;

            // FIXED: Create new trait tracker and copy individual traits
            tracker.traits = new TraitSet(toCopyTo);
            foreach (Trait trait in toCopyFrom.traits.allTraits)
            {
                tracker.traits.GainTrait(new Trait(trait.def, trait.Degree, trait.ScenForced));
            }

            tracker.skinColorOverride = toCopyFrom.skinColorOverride;
            tracker.HairColor = toCopyFrom.HairColor;

            return tracker;
        }

        private static Pawn_SkillTracker CopySkillTracker(Pawn toCopyTo, Pawn_SkillTracker toCopyFrom)
        {
            var tracker = new Pawn_SkillTracker(toCopyTo);

            // FIXED: Create new skills list and copy individual skill records
            tracker.skills = new List<SkillRecord>();
            foreach (SkillRecord skill in toCopyFrom.skills)
            {
                SkillRecord newSkill = new SkillRecord(toCopyTo, skill.def);
                newSkill.Level = skill.Level;
                newSkill.xpSinceLastLevel = skill.xpSinceLastLevel;
                newSkill.xpSinceMidnight = skill.xpSinceMidnight;
                newSkill.passion = skill.passion;
                tracker.skills.Add(newSkill);
            }

            return tracker;
        }

        private static Pawn_HealthTracker CopyRelevantHediffs(Pawn toCopyTo, Pawn copiedFrom)
        {
            var toCopyFrom = copiedFrom.health;
            var tracker = toCopyTo.health;
            // Step 0: Remove everything, Reset 
            tracker.RemoveAllHediffs();
            tracker.Reset();
            // Step 1: Copy ALL Hediffs 
            foreach (Hediff hed in toCopyFrom.hediffSet.hediffs)
            {
                // DevNote: There were a lot of issues around bodyparts: 
                // Some Hediffs really need to know their  bodypart, e.g. an implanted arm can either be left or right. 
                // Ignoring this will lead to many errors, mostly around nullpointers.

                // Issue #130: LoveThrall is a strange Hediff that has a lot of background logic, we skip it 
                if (hed.def.defName == "Hediff_LoveThrall")
                    continue;

                // Issue #184: Copying Pregnancies is super bad, so we do not touch pregnancies
                if (hed.def.defName == RimWorld.HediffDefOf.Pregnant.defName)
                    continue;
                if (PregnancyUtility.GetPregnancyHediff(copiedFrom) != null)
                    if (PregnancyUtility.GetPregnancyHediff(copiedFrom) == hed)
                        continue;

                BodyPartRecord originalBPR = hed.Part;
                if (originalBPR != null) { 
                    BodyPartRecord copyBPR = toCopyTo.RaceProps?.body.AllParts.Find(bpr => bpr.def == originalBPR.def);
                    if (copyBPR != null && !copyBPR.IsMissingForPawn(toCopyTo)) { 
                        Hediff copiedHediff = HediffMaker.MakeHediff(hed.def, toCopyTo, copyBPR);
                        tracker.AddHediff(copiedHediff);
                    }
                } else
                {
                    Hediff copiedHediff = HediffMaker.MakeHediff(hed.def, toCopyTo);
                    tracker.AddHediff(copiedHediff);
                }
            }
            // Step 2: Remove all Artifical Parts
            List<Hediff> hediffsToRemove = new List<Hediff>();
            foreach (Hediff hed in tracker.hediffSet.hediffs)
            {
                if (hed is Hediff_AddedPart && ((Hediff_AddedPart)hed).def.countsAsAddedPartOrImplant)
                {
                    hediffsToRemove.Add(hed);
                }
            }
            tracker.hediffSet.hediffs.RemoveAll(x => hediffsToRemove.Contains(x));

            // Step 3: Tend issues from Removal
            foreach (Hediff copiedHediff in tracker.hediffSet.hediffs)
            {
                if (copiedHediff.Bleeding)
                    copiedHediff.Tended(1.0f,1.0f);
            }
            
            return tracker;
        }

        private static Pawn_StyleTracker CopyStyleTracker(Pawn toCopyTo, Pawn_StyleTracker toCopyFrom)
        {
            var tracker = new Pawn_StyleTracker(toCopyTo);

            tracker.beardDef = toCopyFrom.beardDef;
            tracker.BodyTattoo = toCopyFrom.BodyTattoo;
            tracker.FaceTattoo = toCopyFrom.FaceTattoo;

            return tracker; 
        }

        private static String RandomNamePostFix(int numberOfParts)
        {
            List<String> additions = new List<String>()
            {
                "A","B","C","D","E","F","X","Y","Z",
                "Two",
                "Alpha","Beta","Gamma","Delta","Epsilon","Zeta","Eta","Theta","Iota","Kappa","Lambda","Mu","Nu","Xi","Omicron","Pi","Rho","Sigma","Tau","Upsilon","Phi","Chi","Psi","Omega"
            };

            additions.Shuffle();
            return String.Join(" ",additions.Take(numberOfParts));
        }
    }

}