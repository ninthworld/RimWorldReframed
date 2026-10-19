// Authored static glyph family. SVG masters are source; PNGs are runtime assets.
// Small source silhouettes guide these reconstructions; unshown glyphs are inferred.
const fs = require('fs'), path = require('path'), sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const line = d => `<path d="${d}" fill="none" stroke="white" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>`;
const body = {
 search: '<circle cx="6.6" cy="6.6" r="4.1" fill="none" stroke="white" stroke-width="1.8"/>'+line('M9.8 9.8L14 14'),
 back: line('M7 3L2 8L7 13M2 8H9.5C14.5 8 14.5 3 11 3'),
 clear: line('M4 4L12 12M12 4L4 12'),
 codex: '<path d="M1 3Q4 1 7 3V14Q4 12 1 14ZM9 3Q12 1 15 3V14Q12 12 9 14Z" fill="white"/>',
 compare: '<path d="M1 1H10V5H5V10H1ZM6 6H15V15H6Z" fill="white"/>',
 repair: '<path d="M3 8V14H7V10H10V14H14V8L8.5 3Z" fill="white"/>'+line('M2 6L5 3L7 5M5 3V8'),
 roof: '<path d="M1 8L8 2L15 8H13V14H3V8ZM6 9V12H10V9Z" fill="white" fill-rule="evenodd"/>',
 colonists: '<circle cx="8" cy="8" r="6" fill="none" stroke="white" stroke-width="1.5"/><circle cx="8" cy="5.5" r="2" fill="white"/><path d="M4 12V11C4 7.5 12 7.5 12 11V12Z" fill="white"/>',
 helper: '<path d="M5 1H12C14 1 15 2 15 4V5H12V12C12 14 11 15 9 15H1V12H4V4C4 2 4 1 5 1ZM6 4V5H10V4ZM6 7V8H10V7ZM6 10V11H9V10ZM12 3V4H13.5V3Z" fill="white" fill-rule="evenodd"/>',
 resources: '<path d="M4 2H14V5H4L1 3.5ZM4 6.5H14V9.5H4L1 8ZM4 11H14V14H4L1 12.5Z" fill="white"/>',
 beauty: '<path d="M3 10L9 4L12 7L6 13L1 15Z" fill="white"/>'+line('M11 1V3M14 3L13 4M14 6H15'),
 heat: '<path d="M7 2V10A3 3 0 1 0 10 10V2A1.5 1.5 0 0 0 7 2Z" fill="none" stroke="white" stroke-width="1.5"/>'+line('M8.5 6V12'),
 pollution: '<defs><path id="lobe" d="M5.9 1.1C1.7 3.2 4 7.6 8 7.6C12 7.6 14.3 3.2 10.1 1.1C11.7 3.9 10.3 5.5 8 5.5C5.7 5.5 4.3 3.9 5.9 1.1Z"/><mask id="center-cut"><rect width="16" height="16" fill="white"/><circle cx="8" cy="8" r=".85" fill="black"/></mask></defs><g fill="white" mask="url(#center-cut)"><use href="#lobe"/><use href="#lobe" transform="rotate(120 8 8)"/><use href="#lobe" transform="rotate(240 8 8)"/><circle cx="8" cy="8" r="1.3"/></g>',
 terrain: '<path d="M2 4L5 1H11L14 4V6H10V9L13 12L14 15H2L3 12L6 9V6H2Z" fill="white"/>',
 fertility: '<path d="M7 8C2 8 1 5 2 2C6 2 8 5 7 8ZM9 7C9 3 12 2 15 3C15 7 12 9 9 7ZM3 10.5H13V12H3ZM4 13H12L10.5 15H5.5Z" fill="white"/>'+line('M8 6V10'),
 zones: '<path d="M1 1H10V5H6V10H1ZM7 6H15V15H7Z" fill="white"/>',
 roofs: '<path d="M1 13L6 4H10L15 13Z" fill="white"/>',
 roomstats: '<path d="M2 11H5V15H2ZM6 7H9V15H6ZM10 3H14V15H10Z" fill="white"/>',
 more: '<circle cx="3" cy="8" r="1.4" fill="white"/><circle cx="8" cy="8" r="1.4" fill="white"/><circle cx="13" cy="8" r="1.4" fill="white"/>'
};
async function main() {
 for (const dir of ['Assets/Tools','Textures/Reframed/Tools']) fs.mkdirSync(path.join(root,dir),{recursive:true});
 for (const [name,contents] of Object.entries(body)) {
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16">${contents}</svg>\n`;
  fs.writeFileSync(path.join(root,'Assets/Tools',name+'.svg'),svg);
  await sharp(Buffer.from(svg),{density:432}).resize(96,96).png().toFile(path.join(root,'Textures/Reframed/Tools',name+'.png'));
 }
 console.log(`Rendered ${Object.keys(body).length} tools glyphs.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
