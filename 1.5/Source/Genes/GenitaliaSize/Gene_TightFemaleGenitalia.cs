﻿namespace RJW_Genes
{
    public class Gene_TightFemaleGenitalia : Gene_GenitaliaResizingGene
    {
        public override void Resize()
        {
            var bounds = this.GetResizingBounds();
            SizeAdjuster.AdjustAllVaginaSizes(pawn, bounds.Item1, bounds.Item2);
        }
    }
}