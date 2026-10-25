# Map ground textures v1

Created with the built-in image_gen tool using the project's Basic Holy 3x and Basic Vermin 4x character sheets as style references.

- TempleCourtyard-v1.png: warm grey stone, olive moss, faded gold details for the Holy characters.
- VerminMeadow-v1.png: muted olive grass, brown soil and small stones for the Vermin characters.

These are ground artwork assets, not full gameplay maps. Both PNGs are opaque 1254 x 1254. Import settings: Single Sprite, Full Rect, Point filtering, no mipmaps, no texture compression, Repeat wrapping. Pixels Per Unit is 313.5 so each texture has a natural world size of 4 x 4. Adjust PPU or SpriteRenderer Size to tune detail scale against characters.

To preview, assign either sprite to a separate SpriteRenderer, set Draw Mode to Tiled, and expand Size to cover the camera with a margin. Use the same ground sorting layer as the existing Ground prefab. Tile-sized movement can be added separately for an infinite background.

The images were visually inspected. Seamless edges were requested, but repetition and edge continuity have not been validated in Unity. Inspect a 2 x 2 repeat before production use; Repeat wrapping alone does not guarantee matching edge artwork. The current scene, Ground prefab, and chunk manager have not been modified.

## Generation prompts

### TempleCourtyard-v1.png

Create a production game ground texture, square full bleed opaque PNG, seamless tileable on BOTH horizontal and vertical axes. Use the first attached Holy character sheet and second Vermin character sheet ONLY as style references: tiny retro fantasy pixel art, limited muted palette, chunky crisp square pixels, dark earthy outlines sparingly, visually equivalent to 256x256 logical pixels enlarged nearest-neighbor. Theme: abandoned holy temple courtyard, desaturated warm grey limestone paving, muted olive moss in a few cracks, occasional dusty taupe worn stones, subtle faded ochre decorative stone insets. Strict overhead orthographic flat ground only, no perspective, no walls, no borders, no buildings, no tall props, no characters, no text, no labels, no UI, no watermarks, no light gradients, no vignette, no anti-aliasing. 90 percent unobstructed walkable floor, quiet low contrast, sprites with white/gold armor and brown insects must stand out. Evenly distributed small variation, no central focal point or obvious repeat motif. Edge stones and moss must continue naturally across opposite edges. Deliver one ground texture asset only, not a mockup, not a scene screenshot.

### VerminMeadow-v1.png

Create one production ground texture asset for a top-down 2D fantasy survival game, square full bleed opaque PNG. Seamlessly tileable in both axes, opposite edges connect. Use attached Holy and Vermin sprite sheets ONLY as stylistic references: retro low-resolution pixel art, chunky crisp square pixels, limited muted colors, restrained dark outlines, no smooth painting or anti-aliasing. Equivalent to 256x256 logical pixel detail enlarged nearest-neighbor. Theme: weathered meadow infested with vermin, subdued olive/sage grass with irregular small patches of desaturated earthen brown soil, tiny scattered flat grey pebbles, sparse flattened dry grass. No visible bugs or characters. Ground much quieter and lower contrast than the reference characters so white-gold warriors and brown insects read clearly. Flat directly overhead orthographic texture only; evenly spread detail with 90 percent clear walkable terrain; no central focal point, paths, walls, trees, bushes, large rocks, props, buildings, perspective, sky, lighting gradients, cast shadows, vignette, labels, text, borders, UI, watermark. Continuous terrain at all four boundaries. Deliver ground texture only, not a screenshot or presentation board.

