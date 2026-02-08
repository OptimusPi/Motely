# v0 Design System Brief

Paste the **Prompt block** below at the start of a v0 conversation (or into the project context) so generated UI uses this app’s design system instead of random colors and spacing.

---

## Prompt (copy into v0)

```
Design system for this project. Follow strictly.

**Stack:** React, Tailwind CSS v4, CSS variables in :root/.dark. shadcn/ui new-york style.

**Colors – use only these Tailwind classes (they map to our CSS variables):**
- Surfaces: bg-background, bg-card, bg-popover, bg-muted, bg-accent, bg-secondary, bg-sidebar
- Text: text-foreground, text-muted-foreground, text-primary, text-primary-foreground, text-accent-foreground, text-card-foreground, text-destructive
- Borders/inputs: border-border, border-input, bg-input
- Actions: bg-primary text-primary-foreground, bg-destructive text-destructive-foreground (or white), bg-secondary text-secondary-foreground
- Focus ring: focus-visible:ring-ring focus-visible:ring-2 (or ring-[3px]) focus-visible:ring-offset-2 focus-visible:ring-offset-background
- Do NOT use raw colors like bg-blue-500, text-gray-700, etc. Use semantic names only.

**Radius:** Use rounded-md or rounded-lg (we use --radius: 0.625rem). Avoid rounded-full unless for pills/avatars.

**Spacing:** Prefer Tailwind spacing scale (p-4, gap-3, space-y-2, etc.). No arbitrary values unless necessary.

**Components:**
- Buttons: default (bg-primary), outline (border bg-background hover:bg-accent), secondary (bg-secondary), ghost (hover:bg-accent), destructive (bg-destructive), link (underline).
- Cards: bg-card text-card-foreground border border-border rounded-lg (or rounded-xl).
- Inputs: bg-background border border-input rounded-md, focus-visible:ring-ring.
- Use cn() or classNames for merging (clsx + tailwind-merge pattern).

**Dark mode:** All tokens have .dark overrides; use the same class names (e.g. bg-background), no dark: prefixes for our semantic colors.

**Fonts:** We use Geist (Vercel’s font) for UI: --font-sans and --font-mono (Geist Mono). Prefer font-sans for UI.
```

---

## Token reference (for your own edits)

| Purpose        | CSS variable    | Tailwind class (example)   |
|----------------|-----------------|----------------------------|
| Page background| `--background`   | `bg-background`            |
| Main text      | `--foreground`   | `text-foreground`          |
| Cards/panels   | `--card`        | `bg-card`, `text-card-foreground` |
| Primary action | `--primary`     | `bg-primary text-primary-foreground` |
| Secondary      | `--secondary`   | `bg-secondary text-secondary-foreground` |
| Muted          | `--muted`       | `bg-muted text-muted-foreground` |
| Accent (hover) | `--accent`      | `bg-accent text-accent-foreground` |
| Danger         | `--destructive` | `bg-destructive`           |
| Borders        | `--border`      | `border-border`            |
| Inputs         | `--input`       | `border-input`, `bg-input` |
| Focus ring     | `--ring`        | `ring-ring`                |
| Radius         | `--radius`      | `rounded-lg` (0.625rem)    |

Tokens are defined in `app/globals.css` (`:root` and `.dark`). Tailwind theme is wired in `@theme inline` there. Fonts: [Geist](https://vercel.com/font) (sans) and Geist Mono.

---

## Why this helps v0

v0 doesn’t know your app unless you tell it. Giving it:

1. **Only allowed class names** (semantic: `bg-primary`, not `bg-blue-600`) keeps output on-brand.
2. **One place for tokens** (`app/globals.css`) so you change colors once and v0-generated UI still matches after you paste it in.
3. **Same component rules** (button variants, card style, focus ring) so new screens feel like the rest of the app.

Use the prompt every time you start a new v0 thread or project; you can shorten it to “Use the design system in V0_DESIGN_SYSTEM.md” if v0 has project context.

---

## Porting to WeeJoker.app (or any Next/React app)

1. **Copy the token source:** Copy `app/globals.css` (the `:root`, `.dark`, and `@theme inline` block) into your app's global CSS. That's your single source of truth for colors and radius.
2. **Tailwind:** Use Tailwind v4 `@theme inline` or v3 `theme.extend.colors` mapping to the same CSS variables (e.g. `background: 'var(--background)'`).
3. **Use semantic classes:** In components use `bg-background`, `text-foreground`, `bg-primary`, `border-border`, etc., not raw palette classes.
4. **v0 project (e.g. v0-balatro-seed-hosting):** Run `npm install motely-wasm@1.2.4` in that repo. Use the design-system prompt in v0 so generated UI matches this token set.
