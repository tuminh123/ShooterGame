// Kiểm tra file PNG: chữ ký, kích thước IHDR, số pixel trong suốt/không trong suốt.
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const DIR = join(__dirname, '..', '..', '..', 'Assets', '_Project', 'Graphic', 'Pickups');
const files = ['HealthPotion.png', 'HolyWaterVial.png', 'DungeonKey.png'];

for (const f of files) {
  const buf = await readFile(join(DIR, f));
  const sig = buf.subarray(0, 8).toString('hex');
  const ok = sig === '89504e470d0a1a0a';
  const w = buf.readUInt32BE(16), h = buf.readUInt32BE(20);
  const bitDepth = buf[24], colorType = buf[25];
  // đếm alpha từ IDAT: giải nén để chắc chắn
  const { inflateSync } = await import('node:zlib');
  // tìm IDAT
  let pos = 8, idat = null;
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.subarray(pos + 4, pos + 8).toString('ascii');
    if (type === 'IDAT') idat = buf.subarray(pos + 8, pos + 8 + len);
    if (type === 'IEND') break;
    pos += 12 + len;
  }
  const raw = inflateSync(idat);
  const rowBytes = w * 4 + 1; // 1 byte filter mỗi dòng
  let transparent = 0, opaque = 0, semi = 0;
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const a = raw[y * rowBytes + 1 + x * 4 + 3];
      if (a === 0) transparent++;
      else if (a === 255) opaque++;
      else semi++;
    }
  }
  console.log(`${f}: sig=${ok ? 'OK' : 'SAI'} ${w}x${h} depth=${bitDepth} colorType=${colorType} | trong suốt=${transparent}px, đục=${opaque}px`);
}
