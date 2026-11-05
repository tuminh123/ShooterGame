// Generator sprite pixel-art theo chuẩn dự án (xem Docs/Art/Pickup-Art-Prompts.md):
// - Base 24x24, upscale nearest-neighbor x4 -> 96x96
// - Nền trong suốt, palette hạn chế, viền sẫm 1px, không gradient/glow/AA
// Chạy: node Docs/Art/Tools/gen-pixel-art.mjs
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import zlib from 'node:zlib';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '..', '..', '..');
const OUT_DIR = join(ROOT, 'Assets', '_Project', 'Graphic', 'Pickups');

const SCALE = 4;
const W = 24;
const H = 24;

// ---------- PNG encoder (RGBA, filter 0, zlib) ----------
function crc32(buf) {
  let table = crc32.table;
  if (!table) {
    table = crc32.table = new Int32Array(256);
    for (let n = 0; n < 256; n++) {
      let c = n;
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
      table[n] = c;
    }
  }
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = table[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}

function encodePng(width, height, rgba) {
  const sig = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;  // bit depth
  ihdr[9] = 6;  // color type RGBA
  const raw = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y++) {
    raw[y * (width * 4 + 1)] = 0; // filter none
    rgba.copy(raw, y * (width * 4 + 1) + 1, y * width * 4, (y + 1) * width * 4);
  }
  const idat = zlib.deflateSync(raw, { level: 9 });
  return Buffer.concat([sig, chunk('IHDR', ihdr), chunk('IDAT', idat), chunk('IEND', Buffer.alloc(0))]);
}

// ---------- Grid: vẽ 24x24 từ chuỗi ký tự (căn trái) ----------
function pad(row) {
  if (row.length > W) throw new Error(`Dòng dài ${row.length} (>${W}): "${row}"`);
  return row + '.'.repeat(W - row.length);
}

const PALETTES = {
  health: {
    K: [58, 31, 18, 255],    // outline nâu sẫm
    c: [165, 106, 58, 255],  // nút bần sáng
    C: [142, 90, 51, 255],   // nút bần tối
    d: [110, 21, 21, 255],   // thủy tinh đỏ sẫm
    r: [192, 57, 43, 255],   // đỏ giữa
    R: [224, 69, 53, 255],   // đỏ sáng
    w: [255, 255, 255, 255], // điểm sáng
    p: [255, 201, 201, 255], // phản chiếu hồng nhạt
    s: [255, 223, 194, 255], // ánh sáng trên kính
    b: [74, 42, 26, 255],    // bóng nền
  },
  holy: {
    K: [30, 34, 58, 255],    // outline navy
    d: [40, 52, 92, 255],    // kính xanh sẫm
    b: [52, 84, 148, 255],   // xanh giữa
    B: [74, 130, 200, 255],  // xanh sáng
    w: [255, 255, 255, 255], // điểm sáng
    p: [196, 232, 255, 255], // phản chiếu nhạt
    g: [196, 170, 110, 255], // miệng lọ vàng
    G: [160, 130, 78, 255],  // vàng sẫm
    t: [224, 224, 210, 255], // con dấu trắng ngà
  },
  key: {
    K: [58, 42, 18, 255],    // outline nâu
    g: [214, 168, 74, 255],  // vàng chính
    G: [178, 132, 52, 255],  // vàng bóng
    d: [140, 96, 34, 255],   // vàng sẫm
    w: [255, 238, 180, 255], // điểm sáng
    h: [112, 78, 30, 255],   // bóng
  },
};

// ---------- Thiết kế sprite (24 dòng, căn trái, pad() tự đệm) ----------
const SPRITES = {
  HealthPotion: {
    palette: 'health',
    grid: [
      '',
      '.......KKKKKK',
      '......KccccccK',
      '......KccKKccK',
      '......KKKKKKKK',
      '......KddddddK',
      '......KddRRddK',
      '......KddddddK',
      '.....KddRRRRddK',
      '...KKddRRRRRRddKK',
      '..KddRRRRRRRRddK',
      '.KddRRRRRRRRRRddK',
      '.KddRRRRRRRRRRddK',
      'KddRRRRRRRRRRRRddK',
      'KddRRRRRRRRRRRRddK',
      'KdRRRwwRRRRRRRRRdK',
      'KdRRwwRRRRRRRRRRdK',
      'KdRRRRRRRRRRRRRRdK',
      '.KdRRRRRRRRRRRRdK',
      '.KddRRRRRRRRRRddK',
      '..KddRRRRRRRRddK',
      '...KKddRRRRRRddKK',
      '.....KKddddddKK',
      '.......KKKKKK',
    ],
  },
  HolyWaterVial: {
    palette: 'holy',
    grid: [
      '',
      '......KKKKKKKK',
      '.....KwwKKKKwwK',
      '.....KKKKKKKKKK',
      '.....KggggggggK',
      '.....KGGggggGGK',
      '.....KKgKKKKgKK',
      '......KddddddK',
      '......KdBBBBdK',
      '......KdBBBBdK',
      '.....KdBBBBBBdK',
      '.....KdBbbbbBdK',
      '....KdBBbbbbBBdK',
      '....KdBBbbbbBBdK',
      '...KdBBbbbbbbBBdK',
      '...KdBBbbbbbbBBdK',
      '..KdBBbbbbbbbbBBdK',
      '..KdBBbbbbbbbbBBdK',
      '.KdBBbbbbbbbbbbBBdK',
      '.KdBBbbbbbbbbbbBBdK',
      '.KdBBBBBBBBBBBBBBdK',
      '..KdBBBBBBBBBBBBdK',
      '...KKdBBBBBBBBdKK',
      '.....KKKddddKKK',
    ],
  },
  DungeonKey: {
    palette: 'key',
    grid: [
      '',
      '',
      '....KKKKKKKKKKK',
      '..KKggGGGGGGggKK',
      '.KggGwwwwwwGggK',
      '.KgGwwG....GwwgK',
      '.KgGwwG....GwwgK',
      '.KgGwwG....GwwgK',
      '.KgGwwG....GwwgK',
      '.KgGwwG....GwwgK',
      '.KgGwwG....GwwgK',
      '.KgGwwG....GwwgK',
      '.KggGwwwwwwGggK',
      '.KggGGGGGGGGggK',
      '..KKggGGGGGGggKK',
      '....KKKKKKKKKKK',
      '.......KddddddddddK',
      '......KddddddddddddK',
      '.....KddddddddddddddK',
      '....KddddddddddddddddK',
      '.....KddddddddddddddK',
      '......KddddddddddddK',
      '.......KddddddddddK',
      '........KKKKKKKKKK',
    ],
  },
};

// ---------- Render ----------
function renderSprite(sprite) {
  const palette = PALETTES[sprite.palette];
  const base = Buffer.alloc(W * H * 4);
  sprite.grid.forEach((rowRaw, y) => {
    const row = pad(rowRaw);
    for (let x = 0; x < W; x++) {
      const ch = row[x];
      if (ch === '.') continue;
      const col = palette[ch];
      if (!col) throw new Error(`Màu không biết: "${ch}" ở (${x},${y})`);
      const o = (y * W + x) * 4;
      base[o] = col[0]; base[o + 1] = col[1]; base[o + 2] = col[2]; base[o + 3] = col[3];
    }
  });
  // upscale nearest-neighbor
  const S = SCALE;
  const out = Buffer.alloc(W * S * H * S * 4);
  for (let y = 0; y < H * S; y++) {
    for (let x = 0; x < W * S; x++) {
      const si = (Math.floor(y / S) * W + Math.floor(x / S)) * 4;
      const di = (y * W * S + x) * 4;
      out[di] = base[si]; out[di + 1] = base[si + 1];
      out[di + 2] = base[si + 2]; out[di + 3] = base[si + 3];
    }
  }
  return encodePng(W * S, H * S, out);
}

mkdirSync(OUT_DIR, { recursive: true });
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  for (const [name, sprite] of Object.entries(SPRITES)) {
    const png = renderSprite(sprite);
    const file = join(OUT_DIR, `${name}.png`);
    writeFileSync(file, png);
    console.log(`Đã tạo ${file} (${png.length} bytes)`);
  }
}

export { SPRITES, PALETTES, W, H, pad, renderSprite, encodePng };
