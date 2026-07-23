# CV Match AI — Design System (Stitch Redesign Brief)

## How to use this file in Stitch

This is a **redesign** pass, not a from-scratch generation. Upload the existing screens
(Sign In, Register, Home/Dashboard, Interview Portal, Interview Session, Evaluation Report,
Settings) and apply this document as the consistency layer. Keep every screen's information
architecture, form fields, and functional elements exactly as they are — only transform the
**visual language**: spacing, hierarchy, color use, typography weight, motion, and the amount
of "loudness" per screen. Nothing should be removed or restructured; things should breathe.

---

## 1. Brand personality

Three reference points, one target feeling:

- **Bumble** — confident, warm, one clear action per screen, nothing competing for attention.
- **Spotify** — dark-mode-first depth, bold but restrained accent color, generous negative space, content feels premium even when it's just a list.
- **Instagram** — minimal chrome, content/typography-led, UI gets out of the way instead of decorating itself.

The target is not "dashboard with everything visible." The target is **an elegant jewelry
display case**: one object (one action, one number, one message) presented with enough empty
space around it that it reads as valuable. Every screen should have a single obvious focal
point — not four cards competing at once.

**In one sentence:** *quiet, confident, single-focus SaaS — never a control panel.*

---

## 2. What to actively remove or soften

This is the most important section — the current version fails because it's a "basic card
dashboard" with too much visible simultaneously. When redesigning each screen, actively ask:
"what can I remove, merge, or defer to a secondary state (hover/expand/scroll) so only one
thing is dominant?"

- No screen should show more than **one primary metric or one primary action** at full visual
  weight. Everything else drops to a secondary, quieter tier (smaller type, muted color, less
  contrast, tucked into a scrollable list).
- Stat rows (completed interviews / in-progress / last targeted role / username) should not
  read as four equal boxes shouting at once — pick one hero number, demote the rest to a thin
  inline strip or a single combined card.
- Kill borders-everywhere. Prefer separation through whitespace and subtle shadow, not lines.
- Kill uniform icon-in-colored-circle repeated four times in a row — it reads as a template,
  not a considered layout. Vary scale and emphasis instead.

---

## 3. Color system (keep consistent with current codebase — do not invent a new palette)

The app already has a working theme system; redesign within it, don't replace it.

**Light theme**
- Background: warm cream `#FAF6EE`
- Surface (cards): warm off-white `#FFFDF8`
- Primary text: warm near-black `#2A2620` (never pure black)
- Muted text: `#7A7367`
- Border (used sparingly): `#E8E0D2`

**Dark theme**
- Background: `#1A1D20`
- Surface: `#24282C`
- Primary text: `#E9ECEF`
- Muted text: `#ADB5BD`

**Accent (both themes)** — a single indigo→violet gradient, used sparingly as the one loud
element per screen (primary CTA, active state, one highlighted number):
`linear-gradient(135deg, #4F46E5 0%, #7C3AED 100%)`

**Sidebar / hero surfaces** — fixed dark surface in both themes (this is deliberate, Linear/
Notion-style): `#14151A`

Do not introduce pinks, oranges, or a second accent hue. The "jewelry case" feeling comes from
using the *one* accent gradient rarely and precisely, surrounded by a lot of quiet cream/dark
neutral — not from adding more color.

---

## 4. Typography

- One typeface family, system/sans, already in place — don't swap it.
- Strong size contrast between the hero element and everything else: one big number or
  headline (2–3rem), everything supporting drops to 0.8–0.95rem and muted color.
- Letter-spacing slightly tightened on headlines (already `-0.015em` in the codebase) — keep
  that, it's part of the premium feel.
- Avoid bolding more than one text element per card. Bold = the single thing that matters.

---

## 5. Spacing & layout principles

- Increase whitespace between sections beyond what feels comfortable at first — the jewelry-
  case feeling requires more empty space than a typical dashboard, not less.
- Cards: soft rounded corners (already `0.75–1rem` radius), soft diffused shadow instead of
  hard borders, generous internal padding (`1.5rem+`).
- Prefer a single-column or asymmetric two-column rhythm over 3–4 equal-width grids. Equal
  grids of stat boxes are exactly the "basic card dashboard" look to avoid.
- Sidebar stays minimal: brand mark, 3–4 nav items, one CTA, user chip. No badges, no counters
  cluttering nav items.
- Topbar stays a single thin strip: page title + theme toggle + user chip. Nothing else.

---

## 6. Motion (subtle, never decorative for its own sake)

- Auth hero screens: soft, slow-moving blurred gradient blobs behind the headline (12–18s
  loop, low opacity, `filter: blur`), giving quiet depth without distracting from the text.
  Respect `prefers-reduced-motion`.
- Card hover: gentle lift (translateY ~2px) + soft shadow increase, ~150ms ease.
- No bouncing, no attention-grabbing entrance animations, no confetti. Motion should feel like
  ambient light shifting, not UI performing.

---

## 7. Per-screen redesign notes

**Sign In / Register (split-screen auth)**
Already has left dark hero + blurred gradient blobs + headline, right form card. Keep this
structure. Push further: make the headline the *only* strong element on the left (no stat
badges, no extra icons on the hero side) — brand mark small and quiet, headline large, one
short supporting line, nothing else. Form panel stays clean, single column, generous vertical
spacing between fields (more than default Bootstrap spacing).

**Home / Dashboard**
This is the screen that most needs the "remove, don't add" treatment. One hero panel at top
(welcome + single primary CTA, gradient background). Below it: pick ONE number to feature
large (e.g. total completed interviews) with the rest (in-progress count, last targeted role)
folded into a small muted inline row underneath it, not separate equal-weight cards. CV upload
becomes its own quiet full-width section lower down, not competing with the stats at the top.
Recent interviews: a simple list, not a table — each row minimal (role + date + status dot),
no borders between rows, just spacing.

**Interview Portal (mode/difficulty selection + history)**
Selection cards stay, but only one card should look "selected/active" with the gradient
accent — unselected cards should be nearly invisible (thin border, no icon color) until
hovered or chosen. History table can simplify to a list pattern matching the dashboard's
recent-interviews list for consistency.

**Interview Session (active + evaluation report)**
Active question card is the single focal point on the page — make it larger and quieter
(more padding, no busy borders), progress/timer condensed into a thin unobtrusive strip above
it. Evaluation report screen: report content itself is the hero (typography-led, like reading
an article), download actions demoted to small ghost buttons, not equal-weight primary
buttons.

**Settings**
Treat like a quiet profile page, not a form dump. Avatar + name as a calm header block, then
one section at a time with clear vertical separation (whitespace, not boxed borders) between
"Personal info" and "Security" — they should not look like two identical stacked cards.

---

## 8. Redesign instruction to paste into Stitch alongside this file

> Redesign the uploaded screens using DESIGN.md as the design system. This is a refinement of
> an existing app, not a new app — preserve all existing content, form fields, navigation
> items, and functional elements exactly as they are. Change only the visual language:
> increase whitespace significantly, reduce every screen to a single dominant focal point per
> the "remove, don't add" rules in section 2, apply the cream/dark neutral palette with the
> single indigo-violet gradient accent used sparingly, and give the overall app the quiet,
> confident, single-focus feeling of Bumble, Spotify, and Instagram — like an elegant jewelry
> display case, never a control-panel dashboard. Keep typography hierarchy strong (one big
> element, everything else quiet and muted). Apply consistently across every screen so it
> reads as one coherent product, not a set of unrelated redesigns.
