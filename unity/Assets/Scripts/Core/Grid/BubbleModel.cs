#nullable enable
using System.Threading;

namespace TenGo.Core
{
    /// Bulle de la grille, portage de BubbleModel.swift.
    ///
    /// `Value` (1–9) est immuable. `Id` est une identité stable qui suit la
    /// bulle à travers la gravité et les recompositions (utile au rendu ;
    /// absent du modèle iOS, porté par les SKNode). Non persisté.
    ///
    /// Twists du défi du jour : `IsAnchored` (ne tombe pas), `IsFrozen`
    /// (inutilisable tant qu'un chemin adjacent ne l'a pas dégelée).
    public sealed class BubbleModel
    {
        private static long _counter;

        public BubbleModel(int value, int row, int col, bool isAnchored = false, bool isFrozen = false, int bloom = 0)
            : this(value, row, col, isAnchored, isFrozen, bloom, NextId())
        {
        }

        private BubbleModel(int value, int row, int col, bool isAnchored, bool isFrozen, int bloom, long id)
        {
            Value = value;
            Row = row;
            Col = col;
            IsAnchored = isAnchored;
            IsFrozen = isFrozen;
            Bloom = bloom;
            Id = id;
        }

        public int Value { get; }
        public int Row { get; set; }
        public int Col { get; set; }
        public bool IsAnchored { get; set; }
        public bool IsFrozen { get; set; }

        /// Fleur de dix : multiplicateur de la chaîne qui la contient (0 = aucune).
        public int Bloom { get; set; }

        public long Id { get; }

        /// Identifiant unique de bulle (monotone, non persisté).
        public static long NextId() => Interlocked.Increment(ref _counter);

        /// Copie conforme (même identité).
        public BubbleModel Clone() => new BubbleModel(Value, Row, Col, IsAnchored, IsFrozen, Bloom, Id);

        /// Même bulle avec une autre valeur : position, ancrage, givre, fleur
        /// et identité conservés (booster Mélange).
        public BubbleModel WithValue(int value) => new BubbleModel(value, Row, Col, IsAnchored, IsFrozen, Bloom, Id);

        public override string ToString() => Value + (IsAnchored ? "a" : "") + (IsFrozen ? "f" : "");
    }
}
