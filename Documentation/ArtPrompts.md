# Art prompts (copy and paste, one at a time)

Ready-made prompts for the v0.1 art set. They work in free tools: **ChatGPT**, **Gemini**, **Microsoft Designer /
Bing Image Creator**, **Adobe Firefly** (free credits).

How to use:
1. Open **one new chat** for the whole set (so the style stays consistent).
2. Copy **one** block below, paste it, generate. Do **not** paste the whole ArtStyleGuide.md; it's a document for us,
   not a prompt.
3. Start with **1. Cottage**. Regenerate until you like the look. Every later prompt says "same art style as the
   cottage", which only works in the same chat.
4. Download the largest version of each image. Don't resize or edit anything: background removal, trimming,
   exact pixel size and alignment to the grid are done afterwards by a script.
5. Save each file with the name shown (for example `cottage.png`) into one folder, e.g. `D:\TownBuilderArt\`.

The magenta background is on purpose: it can be removed perfectly, while white or a fake checkerboard can't.
If the tool shades the object pink or purple, add: "The object itself must contain no pink or magenta."

---

## 1. Cottage (do this first: it sets the style) → `cottage.png`

```
Create an original 2D isometric mobile farming game asset: a small cozy cottage with a red roof, cream walls, a wooden door and two small windows, on a 2x2 tile footprint.
Style: colorful premium cartoon game art, bright warm natural colors, clean shapes with a thin darker outline, soft shading, not pixel art, not photorealistic.
View: exact 2:1 isometric angle, orthographic, no perspective. Light from the upper left: left walls lit, right walls about 25% darker, roof brightest.
Composition: one single building, centered, fully visible, nothing cropped. The base is an isometric diamond. No grass, ground or path around it, no shadow outside the base. No text, letters or watermark.
Background: plain solid flat magenta (#FF00FF), no gradient.
```

## 2. Town Hall → `town_hall.png`

```
Same art style, angle, lighting and outline as the cottage. Create a friendly village town hall with a small clock tower, a blue roof and cream stone walls, on a larger 3x3 tile footprint (about 1.5 times the cottage width).
One single building, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

## 3. Barn → `barn.png`

```
Same art style, angle, lighting and outline as the cottage. Create a red wooden farm barn with white trim and a big double door, on a 2x2 tile footprint (same width as the cottage).
One single building, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

## 4. Bakery → `bakery.png`

```
Same art style, angle, lighting and outline as the cottage. Create a warm village bakery with an orange striped awning, a chimney and a display window, on a 2x2 tile footprint (same width as the cottage). No signs or lettering.
One single building, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

## 5. Silo → `silo.png`

```
Same art style, angle, lighting and outline as the cottage. Create a tall metal grain silo with a domed top, light grey and blue, on a small 1x1 tile footprint (half the cottage width, but taller).
One single object, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

## 6. Field (empty) → `field.png`

```
Same art style, angle and lighting as the cottage. Create an empty tilled farm field plot seen from the same 2:1 isometric angle: a flat diamond of dark brown soil with neat rows and a low wooden border, 1x1 tile footprint. Flat, no plants.
Single object, centered, fully visible. No grass around it, no text.
Background: plain solid flat magenta (#FF00FF).
```

## 7–10. Wheat, 4 growth stages → `wheat_1.png` … `wheat_4.png`

Generate four times, changing only the stage text.

```
Same art style, angle and lighting as the cottage. Create wheat plants growing in a 1x1 isometric diamond area, seen from the same 2:1 isometric angle, growth stage [STAGE]. Plants only: NO soil, NO field, NO border; the space between plants is background.
Single object, centered, fully visible, no text.
Background: plain solid flat magenta (#FF00FF).
```

Stages:
* `wheat_1`: "1 of 4: tiny green sprouts"
* `wheat_2`: "2 of 4: short green stalks"
* `wheat_3`: "3 of 4: tall yellow-green stalks"
* `wheat_4`: "4 of 4: ripe golden wheat with full ears"

## 11–14. Corn, 4 growth stages → `corn_1.png` … `corn_4.png`

Same prompt as wheat, replacing "wheat" with "corn". Stages:
* `corn_1`: "1 of 4: tiny green sprouts"
* `corn_2`: "2 of 4: young green corn plants"
* `corn_3`: "3 of 4: tall green corn plants with tassels"
* `corn_4`: "4 of 4: tall corn plants with ripe yellow cobs"

## 15–16. Item icons → `bread.png`, `corn_bread.png`

```
Same cartoon art style and lighting as the cottage. Create a game item icon: [ITEM], front three-quarter view (not isometric), centered, filling most of the image, thin darker outline. No text.
Background: plain solid flat magenta (#FF00FF).
```

Items:
* `bread`: "a single loaf of fresh golden bread"
* `corn_bread`: "a square piece of golden corn bread"

## 17–20. Ground textures → `grass.png`, `sand.png`, `water.png`, `road.png`

AI tools are bad at exact isometric tiles, so ask for a flat **square top-down texture**. The script turns it into the
isometric diamond tile.

```
Create a seamless tileable square texture seen straight from above (top-down, flat, no perspective): [GROUND]. Cartoon game style matching the cottage, bright natural colors, soft detail, no objects, no text, edges must tile seamlessly.
```

Grounds:
* `grass`: "short lush green grass"
* `sand`: "warm light beach sand"
* `water`: "calm clear blue lake water with soft highlights"
* `road`: "light grey cobblestone road"

---

Do a **test set first**: cottage, field, wheat_4, bread and grass. Send those, check them in the game preview, and
only then make the rest.
