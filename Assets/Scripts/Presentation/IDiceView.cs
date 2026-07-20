using System;

namespace Yahtzee.Presentation
{
    /// <summary>Dice presentation contract. M2 fulfils it with 2D sprites (DiceView2D);
    /// M4 swaps in 3D physics dice behind the same interface (TECH_PLAN §5.4). The engine
    /// decides values first — views only animate toward them.</summary>
    public interface IDiceView
    {
        /// <summary>Instantly show values and keep flags (no animation).</summary>
        void SetDice(int[] values, bool[] kept);

        /// <summary>Animate a roll of the non-kept dice toward the engine-chosen values,
        /// then invoke <paramref name="onSettled"/>. Implementations must honor
        /// GameController.AnimationsEnabled (instant when false).</summary>
        void PlayRoll(int[] values, bool[] kept, Action onSettled);

        /// <summary>Whether dice respond to taps (keep/release).</summary>
        void SetInteractable(bool interactable);
    }
}
