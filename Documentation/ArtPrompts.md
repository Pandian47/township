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

## Milestone 4 step 1: Feed Mill, Soybean, feeds

Use the **same chat** as the first set (or upload `cottage.png` and say "match this art style").

### Feed Mill → `feed_mill.png`

```
Same art style, angle, lighting and outline as the cottage. Create a rustic farm feed mill: a wooden mill building with a green roof, a small grain hopper and a chute, a few grain sacks by the door, on a 2x2 tile footprint (same width as the cottage). No signs or lettering.
One single building, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

### Soybean, 4 growth stages → `soybean_1.png` … `soybean_4.png`

Same prompt as wheat (section 7–10), replacing "wheat" with "soybean". Stages:
* `soybean_1`: "1 of 4: tiny green sprouts with two round leaves"
* `soybean_2`: "2 of 4: small leafy green soybean bushes"
* `soybean_3`: "3 of 4: full bushy green soybean plants"
* `soybean_4`: "4 of 4: ripe soybean plants with yellowing leaves and tan bean pods"

### Feed icons → `chicken_feed.png`, `cow_feed.png`

Same icon prompt as bread (section 15–16). Items:
* `chicken_feed`: "a small burlap sack of chicken feed with a yellow band, a few grains spilling out, no lettering"
* `cow_feed`: "a burlap sack of cattle feed with a green band, a few pellets spilling out, no lettering"

## Milestone 4 step 2: animal buildings, egg, milk

### Chicken Coop → `chicken_coop.png`

```
Same art style, angle, lighting and outline as the cottage. Create a cute wooden chicken coop with a small ramp, a nesting box with straw, a low fenced run and three white chickens around it, on a 2x2 tile footprint (same width as the cottage). No signs or lettering.
One single object, centered, fully visible, nothing cropped. No grass or ground outside the coop's own fenced base, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

### Dairy Shed → `dairy_shed.png`

```
Same art style, angle, lighting and outline as the cottage. Create a small white dairy shed with a dark grey roof, an open stall with hay, a milk churn by the door and one friendly black-and-white cow standing beside it, on a 2x2 tile footprint (same width as the cottage). No signs or lettering.
One single object, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

### Icons → `egg.png`, `milk.png`

Same icon prompt as bread (section 15–16). Items:
* `egg`: "two fresh cream-colored eggs in a little straw nest"
* `milk`: "a glass bottle of fresh milk with a blue cap, no label text"

## Milestone 4 step 3: Sugar Mill, Sugarcane, sugar

### Sugar Mill → `sugar_mill.png`

```
Same art style, angle, lighting and outline as the cottage. Create a small brick sugar mill with a plum-red roof, a short chimney, a big wooden press wheel on the side and bundles of sugarcane by the door, on a 2x2 tile footprint (same width as the cottage). No signs or lettering.
One single building, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

### Sugarcane, 4 growth stages → `sugarcane_1.png` … `sugarcane_4.png`

Same prompt as wheat (section 7–10), replacing "wheat" with "sugarcane". Stages:
* `sugarcane_1`: "1 of 4: short green shoots"
* `sugarcane_2`: "2 of 4: knee-high green canes with long leaves"
* `sugarcane_3`: "3 of 4: tall segmented green canes"
* `sugarcane_4`: "4 of 4: very tall ripe yellow-green segmented canes with leafy tops"

### Icon → `sugar.png`

Same icon prompt as bread (section 15–16). Item: "a small pile of white sugar cubes with a few loose sugar grains"

## Milestone 4 step 4: Creamery, cookies, cake, butter, cheese

### Creamery → `creamery.png`

```
Same art style, angle, lighting and outline as the cottage. Create a small cream-white village creamery with a light blue roof, a big wooden butter churn by the door, milk cans and a round cheese wheel on a bench, on a 2x2 tile footprint (same width as the cottage). No signs or lettering.
One single building, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

### Icons → `cookies.png`, `cake.png`, `butter.png`, `cheese.png`

Same icon prompt as bread (section 15–16). Items:
* `cookies`: "two round golden chocolate-chip cookies, slightly overlapping"
* `cake`: "a round two-layer strawberry cream cake with pink icing and a cherry on top"
* `butter`: "a block of yellow butter on a small blue dish"
* `cheese`: "a wedge of yellow cheese with round holes"

## Milestone 5 step 2: Train Station

The station is wider than other buildings: a **3x2** footprint (1.25× the cottage width). Make **two images of the same
station**, one with an empty platform and one with the train waiting. Generate the second right after the first, in the
same chat, and say "the same station as before".

### Train Station, empty → `train_station.png`

```
Same art style, angle, lighting and outline as the cottage. Create a small countryside train station: a cosy station building with a blue roof and a clock, a wooden platform with a bench and a lamp, and a short straight railway track along the front-left side, on a wide 3x2 tile footprint (about 1.25 times the cottage width). The platform is empty, no train. No signs or lettering.
One single object, centered, fully visible, nothing cropped. No ground around it, no shadow outside the base, no text.
Background: plain solid flat magenta (#FF00FF).
```

### Train Station with the train → `train_station_train.png`

```
The exact same train station as the previous image, same size, angle and position, but now a short colorful cartoon freight train (a red locomotive and two wagons with wooden crates) is waiting on the track along the platform. No signs or lettering.
Background: plain solid flat magenta (#FF00FF).
```

## Milestone 5 step 3: building materials

Same icon prompt as bread (section 15–16). Items:
* `planks`: "a small stack of three light wooden planks"
* `bricks`: "a small stack of red clay bricks"
* `nails`: "three shiny steel nails lying crossed"

## Milestone 5 step 4: land for sale

### "For sale" sign → `for_sale_sign.png`

```
Same art style, lighting and outline as the cottage. Create a small wooden signpost: one post with a rectangular wooden board showing a big golden coin symbol (no letters), seen from the front at a slight isometric angle. Single object, centered, fully visible, no ground, no text.
Background: plain solid flat magenta (#FF00FF).
```

## Railway

### Rail texture → `rail.png` (optional; turned into the isometric rail tile)

```
Create a seamless tileable square texture seen straight from above (top-down, flat, no perspective): a single railway track running exactly vertically through the middle, two steel rails on dark wooden sleepers on a light gravel bed, with short green grass on both sides. Cartoon game style matching the cottage, no text. Top and bottom edges must tile seamlessly.
```

## Milestone 6 step 2: the moving train → `train.png`

The train drives along the railway towards the **upper left**. It must be one straight line of cars seen from the
same 2:1 isometric angle, with the locomotive at the **upper-left** end.

```
Same art style, angle, lighting and outline as the cottage. Create a short cartoon freight train seen from the same 2:1 isometric angle: a red steam locomotive at the upper-left end, followed by a blue wagon and a green wagon loaded with wooden crates, all in one straight line running diagonally from the lower right to the upper left. Wheels visible, no rails, no ground, no smoke, no text or lettering.
Single object, fully visible, nothing cropped.
Background: plain solid flat magenta (#FF00FF).
```

## Milestone 6 step 3: townsfolk and a car

### Townsfolk → `person_1.png` … `person_4.png`

```
Same cartoon art style, lighting and outline as the cottage. Create one small cheerful townsperson walking, full body, seen from the front at a slight angle from above, [PERSON]. Simple shapes that stay readable when very small. No ground, no shadow, no text.
Background: plain solid flat magenta (#FF00FF).
```

People:
* `person_1`: "a farmer in a red shirt and blue overalls with a straw hat"
* `person_2`: "a woman in a blue dress carrying a basket of bread"
* `person_3`: "a boy in a yellow t-shirt and shorts"
* `person_4`: "an older man in a green jumper with a walking stick"

### Car → `car_front.png`, then `car_back.png` (same chat)

```
Same art style, angle, lighting and outline as the cottage. Create a small cute rounded cartoon car (light blue) seen from the same 2:1 isometric angle, driving towards the lower left, so its front with headlights is visible. Wheels visible, no road, no shadow, no text or number plate.
Background: plain solid flat magenta (#FF00FF).
```

Then: "The exact same car, same angle and size, but now driving away towards the upper right, so its back with red tail lights is visible."

---

Do a **test set first**: cottage, field, wheat_4, bread and grass. Send those, check them in the game preview, and
only then make the rest.
