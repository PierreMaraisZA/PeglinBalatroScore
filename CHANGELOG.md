# Changelog

## 1.1.0

- New `BalatroScoreApi` for other mods: a mod can give the HP the flames and OVERKILL aim at (a value it gives is used exactly, so a boss with lowered HP works too), hide the score, the OVERKILL stamp or the bomb chest, and be told the total and target of every shot and of each turn's bombs. With no mod registered, nothing changes.
- When an act's boss fight has no enemy flagged as a boss, Cruciball 6 to 9 now uses the higher HP the game gives those enemies (it was only applied from Cruciball 10).
- The score's shake and the OVERKILL wobble use the mod's own random numbers instead of the game's, so the mod never changes how a seed plays out.
- Fixed the real total missing the damage Peglin's attack adds as it goes out (Echo orbs, Echo Chamber, attack-only bonuses).
- A shot that kills every enemy before it finishes now shows its total instead of vanishing.
- A bomb with no enemy to hit no longer counts towards the bomb chest's total.
- Turning the bomb chest off during a turn's bombs keeps it gone until they're all thrown, instead of bringing back a half-empty chest.

## 1.0.2

- A see-through bomb chest behind Peglin catches a bomb the moment each one is lit, while the orb is still bouncing. It holds about ten, and bombs beyond that heap up and spill over the sides. When the last bomb Peglin throws detonates, the chest bursts on that same frame: every bomb left inside goes off, the chest blows apart into planks, and the bombs' total damage (each bomb's damage after relics, added up) bursts out of the wreckage and shatters.
- The score HUD follows PeglinFonts' current font, including when the font is changed with its hotkey.
- Fixed the boss HP target on fights with a barricade (the Mole's tree) by ignoring the barricade's health.
- Pausing the game freezes the bomb chest instead of letting bombs drop and burst behind the pause menu.

## 1.0.1

- Repackaged 1.0.0; no changes to the mod.

## 1.0.0

- First release: a Balatro-style pegs x damage score for each shot, the game's real total at the end, flames that grow against the act boss's max HP with no upper limit, a flaming OVERKILL (or green OVERHEAL) stamp, and a hover tooltip explaining the maths.
