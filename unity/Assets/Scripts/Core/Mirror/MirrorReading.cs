#nullable enable
namespace TenGo.Core
{
    /// Une aptitude sur une fenêtre de parties, face à la fenêtre précédente.
    public readonly struct MirrorReading
    {
        public MirrorReading(double? value, double? previous)
        {
            Value = value;
            Previous = previous;
        }

        public double? Value { get; }
        public double? Previous { get; }

        /// Progrès relatif dans le bon sens (positif = mieux), `null` sans base.
        public double? Improvement(bool lowerIsBetter)
        {
            if (Value is not double value || Previous is not double previous || previous <= 0) return null;
            double delta = (value - previous) / previous;
            return lowerIsBetter ? -delta : delta;
        }
    }
}
