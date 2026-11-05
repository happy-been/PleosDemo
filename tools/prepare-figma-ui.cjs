// Rasterize original SVGs without changing their viewBox, paths, colours, or proportions.
// Usage: node tools/prepare-figma-ui.cjs <path-to-sharp-package>
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require(process.argv[2] || 'sharp');
const root = path.resolve(__dirname, '../unity/InputProbe/Assets/_Parkside/UI/Art');
async function main() {
  const rows = [];
  for (const name of fs.readdirSync(root).filter(n => n.endsWith('.svg'))) {
    const source = path.join(root, name);
    const metadata = await sharp(source).metadata();
    await sharp(source, { density: 288 }).png().toFile(source.replace(/\.svg$/, '.png'));
    rows.push({ source: name, width: metadata.width, height: metadata.height });
  }
  const photos = fs.readdirSync(root).filter(n => /World.*\.png$/.test(n));
  const hashes = photos.map(name => ({ name, sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(root, name))).digest('hex') }));
  fs.writeFileSync(path.join(__dirname, 'figma-ui-art-report.json'), JSON.stringify({ svgRasterization: rows, photos: hashes }, null, 2) + '\n');
  console.log(`Rasterized ${rows.length} original SVGs; checked ${hashes.length} photographs.`);
}
main().catch(e => { console.error(e); process.exitCode = 1; });
