# Required Graphical Assets — Stardew Economy Project (CP)

All images must be pixel art in the **Stardew Valley 1.6** visual style.
Save as **32-bit RGBA PNG** with transparency where needed.

---

## 1. `big-craftables.png` — Big Craftable Sprites

| Property | Value |
|----------|-------|
| **Dimensions** | **64 × 32 px** |
| **Cell size** | 16 × 32 px each (standard SDV big craftable size) |
| **Layout** | 4 sprites in a horizontal row |

| Cell Index | X Offset | Item | Art Direction |
|:----------:|:--------:|------|---------------|
| 0 | 0 px | **Contract Board** | Wooden bulletin board with pinned papers/notices, cork board texture, maybe a small "CONTRACTS" header sign |
| 1 | 16 px | **Market Terminal** | Electronic monitor on a stand with a small LCD screen showing a chart/graph, futuristic-retro style (like the vanilla computer) |
| 2 | 32 px | **ATM Machine** | Boxy ATM with a small screen, card slot, and keypad; metal/grey body with a banking logo accent color |
| 3 | 48 px | **Supercomputer** | Advanced monitor with a multicoloured heatmap grid on screen, antennae/satellite dish on top, sleek dark casing — clearly more sophisticated than the Market Terminal |

> The top 16 px of each cell is the upper half of the object, bottom 16 px is the base.
> Match the proportions and shading style of vanilla big craftables (e.g., Keg, Preserves Jar).

---

## 2. `skill-icons.png` — Reputation Skill & Profession Icons

| Property | Value |
|----------|-------|
| **Dimensions** | **128 × 16 px** |
| **Cell size** | 16 × 16 px each |
| **Layout** | 8 icons in a horizontal row |

| Cell Index | X Offset | Icon | Art Direction |
|:----------:|:--------:|------|---------------|
| 0 | 0 px | **Reputation Skill Icon** (main) | Gold/amber handshake or trade scales symbol — represents economic reputation. Used in the skills page level-up popup. |
| 1 | 16 px | **Skills Page Icon** (small) | Miniature version of the main icon. Only the **top-left 10×10 px** are used by SpaceCore for the skills page sidebar. Center the art in the top-left 10×10 region. |
| 2 | 32 px | **Local Merchant** profession | Small shop storefront / market stall icon |
| 3 | 48 px | **Bulk Trader** profession | Crate, barrel, or stack of goods icon |
| 4 | 64 px | **Regional Mogul** profession | City skyline / building icon (represents Zuzu City contracts) |
| 5 | 80 px | **Market Manipulator** profession | Chart with trend arrow / graph manipulation icon |
| 6 | 96 px | **Global Exporter** profession | Ship or globe icon (international trade) |
| 7 | 112 px | **Luxury Brand** profession | Diamond, crown, or star icon (premium quality branding) |

> All icons should use a **warm gold/amber (#B48C3C)** accent palette to match the skill's
> experience bar color. Follow the visual style of vanilla profession icons (Farming, Mining, etc.).

---

## Color Reference

| Element | Hex | RGB |
|---------|-----|-----|
| Skill accent / XP bar | `#B48C3C` | (180, 140, 60) |
| Profession accent | `#C8A050` | (200, 160, 80) |
| Background transparency | — | Alpha = 0 |

---

## File Checklist

- [ ] `assets/big-craftables.png` — 64×32, 4 big craftable sprites
- [ ] `assets/skill-icons.png` — 128×16, 8 skill/profession icons
