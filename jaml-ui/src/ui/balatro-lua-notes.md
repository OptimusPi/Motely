# Balatro Lua reference (local install only)

Source on this machine: `/Applications/Balatro.app/Contents/Resources/game/`  
Read-only notes for jaml-ui cozy feel. **Do not commit game assets or Lua.**

## Palette (`globals.lua` → `G.C`)

These are **Lua pre-shader** values. Jimbo tokens stay **eyedropped post-shader** where they already differ — close, not identical on purpose.

| Role | Lua HEX | Jimbo token |
|------|---------|-------------|
| RED / MULT | `FE5F55` | `--j-red` `#fe5148` |
| BLUE / CHIPS | `009dff` | `--j-blue` `#0093ff` |
| GREEN | `4BC292` | `--j-green` `#429f79` |
| ORANGE | `fda200` | `--j-orange` `#ff9800` |
| GOLD / MONEY | `eac058` / `f3b958` | `--j-gold` `#e4b643` |
| PURPLE | `8867a5` | `--j-purple` `#9e74ce` |
| BLACK (UI) | `374244` | near `--j-dark-grey` / darkest family |
| GREY | `5f7377` | text grey family (we brightened for contrast) |
| UI.BACKGROUND_DARK | teal-slate family | panel / surface |

UI defaults (`engine/ui.lua`): ROOT uses `BACKGROUND_DARK` fill + `OUTLINE_LIGHT`; text defaults light.

## Juice (`engine/moveable.lua` → `juice_up`)

- Default amount `0.4`, duration `~0.4s`
- Scale dips then oscillates (`sin` decay cubic)
- Hover zoom `+0.05` scale when `zoom` role set; drag `+0.1`
- Shadow height on moveables `0.2` (chunky under-lip, not soft blur)

DOM translation: press `translateY` + solid south shadow collapse (already `.j-btn`), optional short scale juice on click — not CSS-blur “elevation.”

## Layout feel

- Padding from `G.UIT.padding` (tight, grid of columns/rows)
- Panels: solid fill, hard outline, content can bleed past edges
- No glassmorphism; background contrast eases over ~0.6s on state change

## What “cozy” means for Jimbo

1. Chunky south edge + hard silver outline  
2. Pixel font + hard text shadow  
3. Press-down buttons, not opacity fades  
4. Sunken insets for fields, raised panels for chrome  
5. Gold/red hit feedback, not muted SaaS greys  
