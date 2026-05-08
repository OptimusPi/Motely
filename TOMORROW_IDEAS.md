# 17 Jimbo Components for Tomorrow (Or Whenever)

> A relaxed, low-stakes wishlist. None of these are urgent. Pick whichever
> sounds fun. Delete the rest. Build none of them and the world keeps spinning.

---

## The Practical Seven

These plug holes that every component library eventually needs. Boring
in the best way — once they exist, you stop reinventing them.

1. **`JimboToast`** — chunky pop-in notification. Same 3D-press / drop-shadow
   language as `j-btn`, but auto-dismisses and stacks. Variants: `success`,
   `error`, `coin` (gold). Lives at the top of viewport, not blocking content.

2. **`JimboToggle`** — pixel on/off switch. The handle is a tiny chip with
   the same chunky shadow as buttons; the track flips green/grey. Good for
   the "Show Joker | Show Tarot" type filters.

3. **`JimboSlider`** — range slider where the thumb is a Jimbo chip. Think
   ante number, search threshold, animation speed. Snap stops optional.

4. **`JimboAccordion`** — collapsible panel with the gold-on-dark heading
   bar from your existing `j-panel` system. Clean way to fold the
   filter/options sections in the IDE.

5. **`JimboTabsRing`** — horizontal tab strip but each tab is a pill
   button with the press animation. Inactive tabs sit slightly lower
   (depressed); active tab sits up. Use it instead of the current
   `JamlIdeToolbar` chrome where it makes sense.

6. **`JimboDataGrid`** — the one you actually mentioned wanting. Pixel-y
   table, sticky header, magnetic snap rows, virtualized for 10k+ seeds.
   Cells render either text, sprite, or a custom slot.

7. **`JimboTooltip`** — small speech-bubble tooltip lifted from
   `JammySpeechBox`. Hovers over icons, fades in delayed, never blocks
   the cursor. (Could also be a `<JammySays text="..." />` if you want
   her to do double duty.)

---

## The Game-Feel Six

These exist mostly so the UI feels like *Balatro* and not like every
other dashboard. Jimbo is allowed to have fun.

8. **`JimboCoinFlip`** — when something needs a yes/no with drama. Spinning
   gold-coin animation that lands on a value. Works as a decision UI *or*
   a cute loading spinner.

9. **`JimboHeartMeter`** — health-bar-style progress meter, but the segments
   are little hearts/diamonds/clubs/spades that fill in. Use for things
   like "matched 4/8 criteria."

10. **`JimboDealHand`** — animation primitive that deals N cards from
    off-screen into a target row. Wraps `JamlGameCard`. Stagger,
    spring-bounce on landing. Pure delight, zero use case until you
    suddenly need it everywhere.

11. **`JimboSpinReel`** — slot-machine reel spinner. Useful for randomizing
    a seed, a deck, a vibe. Sound optional.

12. **`JimboConfetti`** — when a search finds a perfect seed, throw confetti.
    Particle system, performant, suit-shaped particles. Fires once on a
    trigger prop.

13. **`JimboMarquee`** — scrolling pixel-text strip across the bottom of
    a panel. *"NOW SEARCHING ANTE 8 ✦ 12,449 SEEDS / SEC ✦ JIMBO IS PROUD"*

---

## The Wildcard Four

Less component, more vibe. Skip if not feeling it.

14. **`JimboKittyPaws`** 🐾 — tap targets shaped like little paw prints,
    leaves a fading trail when you tap. (Co-credit: kittypaws.art.) Could
    be the navigation cursor for a Lola Mode.

15. **`JimboLuckyCat`** — purely decorative animated lucky-cat that lives
    in a corner and waves when good things happen (search complete, seed
    saved, Lola sighted). For Mom.

16. **`JimboGlitch`** — text component that occasionally drops a frame on
    its characters. For dramatic moments. *Use sparingly or not at all.*
    A ridiculous amount of fun if you build it once.

17. **`JimboPi`** — a hidden component that, somewhere very deep in the
    component tree, leaves a tiny `π` somewhere only you can find. Easter
    egg signature. Doesn't render anything visible without a secret prop.

---

## The Wind-Up Bonus

*Tomorrow-or-later. Do not start tonight. Just dreaming on paper.*

18. **`JimboMusicBox`** 🎶 — a wind-up music box component. Instead of
    rendering JAML `should` clauses as a *table*, it plays them back as
    MIDI tones. Each clause becomes a note. You *hear* your search criteria.

    Sketch:
    - Web Audio + a SoundFont 2 (`.sf2`) sample bank for that soft Zelda-64 /
      SNES ding-ding feel. Candidate libs: `spessasynth`, `sf2synth-js`,
      `WebAudioFont`. Default voice: a celesta or music-box sample.
    - Visual: a literal little wind-up box. Crank turns while it plays.
      Cards/sprites on the comb pluck as their note triggers.
    - Mapping: clause type → octave (jokers low, tarots mid, vouchers high).
      Clause value → note within the octave. Order = sequence.
    - Ding ding pretty. ✨

    Why it earns its place: nothing else in the Balatro-tooling ecosystem
    sounds. Letting your filter *play* is the kind of thing only pifreak
    would build.

---

## How to pick

If you're tired tomorrow → start with **#7 JimboTooltip**. Tiny, immediate
payoff, plays nice with what's already there.

If you're feeling gold-energy → **#10 JimboDealHand** is the one that will
make you feel like the project is real. Cards flying in is *the* Balatro
moment.

If you're sad → **#15 JimboLuckyCat**. Build a tiny waving cat for Lola.
Ship one component just for love.

---

## Notes from tonight (so you don't forget)

- `0.26.1` is published locally with the radial menu working. Pull on Windows.
- `Jammy` (SeedMascot) is now importable for the first time. Art by Little
  Miss pifreak. She gets to credit herself however she wants.
- Storybook on `localhost:3141` for tunnel reach.
- The ESM `.js` rule is *because we publish*, not because TypeScript demands.
  If you ever forget, switch tsconfig to `nodenext` and TS will scream at you.
- Don't work past 10. You said.

---

*Drafted by Claude. Co-approved in spirit by Clod, Water Bear, and one
extremely soft-pawed kittypaw.*

🫶 — for pifreak
