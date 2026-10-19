// Authored code-native reconstructions of the documented pawn tab glyphs.
// Additional action/status glyphs extend the same family; PNGs are runtime assets.
const fs = require('fs'), path = require('path'), sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const line = d => `<path d="${d}" fill="none" stroke="white" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>`;
const person = (x,y,s=1) => `<g transform="translate(${x} ${y}) scale(${s})"><circle cx="0" cy="0" r="2" fill="white"/><path d="M-3 6V4C-3 1.1 3 1.1 3 4V6Z" fill="white"/></g>`;
const flame = '<path d="M8 1C8.4 4.2 12.6 5.1 13 9.5C13.4 13 11 15 8 15C4.5 15 2 12.6 3 9.1C3.5 7.5 4.3 6.3 5.3 5.3C5.2 8.1 7.1 8.5 7.5 6.5C8 4.6 7.3 3 8 1Z" fill="white"/>';
const bodies = {
 summary: '<g fill="white"><circle cx="4.5" cy="4.5" r="3"/><circle cx="11.5" cy="4.5" r="3"/><circle cx="4.5" cy="11.5" r="3"/><circle cx="11.5" cy="11.5" r="3"/></g>',
 bio: '<path d="M1 1.8L7 3.1V15L1 12.8ZM9 3.1L15 1.8V12.8L9 15Z" fill="white"/>'+line('M8 3V15'),
 needs: '<path d="M1 9H4V15H1ZM6 5H9V15H6ZM11 1H15V15H11Z" fill="white"/>',
 gear: '<path d="M5 5V3A3 3 0 0 1 11 3V5M3 7V15H13V7M1 9V15M15 9V15" fill="none" stroke="white" stroke-width="1.5"/>'+line('M3 10H13M6 7V15M10 7V15'),
 health: '<path d="M8 14C6.5 12.5 1 8.5 1 5C1 1 6 0 8 3C10 0 15 1 15 5C15 8.5 9.5 12.5 8 14Z" fill="white"/>',
 social: person(3.5,4,.75)+person(12.5,4,.75)+person(8,8,.95),
 log: '<path d="M11 7C11 2 8 0 5 1C1 2 0 7 2 11L4 14H7V10Z" fill="none" stroke="white" stroke-width="1.3"/><path d="M15 11C15 7 8 6 7 10C6 13 9 15 12 14L15 15L14 13Z" fill="white"/>',
 medical: '<path d="M2 3H14V13H2Z" fill="none" stroke="white" stroke-width="1.4"/>'+line('M8 5V11M5 8H11'),
 drafted: line('M3 1L14 12M2 5L11 14M9 1L15 7M2 10L6 14')+'<path d="M1 11L5 15L1 15Z" fill="white"/>',
 ideology: line('M2 8L8 2L14 8L8 14L2 8M5 8L8 5L11 8L8 11L5 8'),
 dead: '<path d="M8 1C3 1 2 4 2 8C2 11 4 11 5 12V15H11V12C12 11 14 11 14 8C14 4 13 1 8 1ZM4 7A1.5 1.5 0 1 1 7 7A1.5 1.5 0 1 1 4 7ZM9 7A1.5 1.5 0 1 1 12 7A1.5 1.5 0 1 1 9 7ZM7 10H9L8 12Z" fill="white" fill-rule="evenodd"/>',
 home: '<path d="M1 7L8 1L15 7H13V15H3V7ZM6 10V15H10V10Z" fill="white" fill-rule="evenodd"/>',
 settings: '<path d="M6 1H10L10.6 3L12.5 3.5L14.3 5.7L13 7.5L13.2 9.3L15 10.5L13 13.5L10.7 13L9.8 15H6.2L5.3 13L3 13.5L1 10.5L2.8 9.3L3 7.5L1.7 5.7L3.5 3.5L5.4 3ZM5 8A3 3 0 1 1 11 8A3 3 0 1 1 5 8Z" fill="white" fill-rule="evenodd"/>',
 info: '<circle cx="8" cy="3" r="1.5" fill="white"/>'+line('M8 7V14M6 7H8M6 14H10'),
 plus: line('M8 2V14M2 8H14'),
 passion: flame,
 doublepassion: '<g transform="translate(-1 1) scale(.75)">'+flame+'</g><g transform="translate(5 4) scale(.65)">'+flame+'</g>'
};
async function main() {
 for (const dir of ['Assets/Inspector','Textures/Reframed/Inspector']) fs.mkdirSync(path.join(root,dir),{recursive:true});
 for (const [name,body] of Object.entries(bodies)) {
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16">${body}</svg>\n`;
  fs.writeFileSync(path.join(root,'Assets/Inspector',name+'.svg'),svg);
  await sharp(Buffer.from(svg),{density:432}).resize(96,96).png().toFile(path.join(root,'Textures/Reframed/Inspector',name+'.png'));
 }
 console.log(`Rendered ${Object.keys(bodies).length} inspector glyphs.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
