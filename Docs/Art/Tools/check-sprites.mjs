// Hiển thị ASCII preview của các sprite 24x24 để kiểm tra hình dạng.
// Chạy: node Docs/Art/Tools/check-sprites.mjs
import { SPRITES, PALETTES, W, pad } from './gen-pixel-art.mjs';

function charFor(ch, palette) {
  if (ch === '.') return ' ';
  const c = palette[ch];
  if (!c) return '?';
  // ánh xạ gần đúng sang ký tự: trắng/pale -> '+', sáng -> '*', tối -> '#', trung -> 'o'
  const [r, g, b] = c;
  const lum = (r + g + b) / 3;
  if (lum > 230) return '+';
  if (lum > 140) return '*';
  if (lum > 80) return 'o';
  return '#';
}

for (const [name, sprite] of Object.entries(SPRITES)) {
  const palette = PALETTES[sprite.palette];
  console.log(`\n=== ${name} (${sprite.palette}) ===`);
  sprite.grid.forEach((rowRaw, y) => {
    const row = pad(rowRaw);
    let line = String(y).padStart(2, '0') + ' ';
    for (let x = 0; x < W; x++) {
      line += charFor(row[x], palette);
      if ((x + 1) % 8 === 0) line += '|';
    }
    console.log(line);
  });
}
