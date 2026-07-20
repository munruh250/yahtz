namespace Yahtzee.Core
{
    /// <summary>Injected RNG. Production uses <see cref="SeededRandomSource"/>; tests inject
    /// scripted sequences. The engine is the only caller — presentation never draws values.</summary>
    public interface IRandomSource
    {
        /// <summary>A uniformly random die face, 1–6.</summary>
        int NextDie();
    }
}
