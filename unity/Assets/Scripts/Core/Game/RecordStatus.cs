#nullable enable
using System;

namespace TenGo.Core
{
    /// Position du score face au record local, calculée AVANT l'insertion du
    /// score. Structure : l'égalité compare tous les champs.
    public readonly struct RecordStatus
    {
        private RecordStatus(RecordStatusKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }

        public RecordStatusKind Kind { get; }
        /// NearMiss : points manquants ; Best : le record.
        public int Value { get; }

        public static readonly RecordStatus None = new RecordStatus(RecordStatusKind.None, 0);
        public static readonly RecordStatus NewRecord = new RecordStatus(RecordStatusKind.NewRecord, 0);
        public static RecordStatus NearMiss(int gap) => new RecordStatus(RecordStatusKind.NearMiss, gap);
        public static RecordStatus Best(int best) => new RecordStatus(RecordStatusKind.Best, best);

        /// Écart au record s'il est « presque battu » : 5 % du record, plancher
        /// 20 points (GameScene.nearMissGap). `null` sinon.
        public static int? NearMissGap(int score, int? best)
        {
            if (best is not int b || b <= 0 || score <= 0) return null;
            int gap = b - score;
            if (gap <= 0) return null;
            return gap <= Math.Max(20, b / 20) ? gap : (int?)null;
        }

        /// Statut d'un score face au meilleur score connu (`null` : aucun).
        public static RecordStatus Of(int score, int? best)
        {
            if (score > 0 && (best == null || score >= best)) return NewRecord;
            int? gap = NearMissGap(score, best);
            if (gap != null) return NearMiss(gap.Value);
            return best != null ? Best(best.Value) : None;
        }

        public override string ToString() => Kind + (Value != 0 ? "(" + Value + ")" : "");
    }
}
