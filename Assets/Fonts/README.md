Drop the game's typeface here as a `.ttf` (or `.otf`), then run
**Yahtzee > Build Font Asset** — or just **Yahtzee > Setup Project**, which calls it.

That generates `Assets/Resources/Fonts/GameFont.asset`, which `UiBuilder` loads at runtime and
applies to every piece of text in the game. Until a font is present the game falls back to TMP's
default face, so a missing font is a downgrade rather than a break.

The generated asset falls back to TMP's default font for glyphs the face lacks. That matters
here: the wall samplers use German umlauts (`täglich`) and the top bar uses a hamburger glyph,
and a handwriting face often has neither. A missing glyph renders as a tofu box — that has
already happened once, with a checkmark in the roll counter.
