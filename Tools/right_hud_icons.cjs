// Authored vector masters; concept silhouettes for bed/person/medicine/heat/check,
// coherent inferred symbols for other native families. No platform emoji.
const fs=require('fs'),path=require('path'),sharp=require('sharp');
const root=path.resolve(__dirname,'..');
const line=d=>`<path d="${d}" fill="none" stroke="white" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>`;
const circle=(x,y,r)=>`<circle cx="${x}" cy="${y}" r="${r}" fill="white"/>`;
const glyphs={
 bed:'<path d="M2 3H14V13H12V11H4V13H2ZM4 5V7H7V5ZM9 5V7H12V5ZM4 8V9.5H12V8Z" fill="white" fill-rule="evenodd"/>',
 joy:'<circle cx="8" cy="8" r="6" fill="none" stroke="white" stroke-width="1.5"/>'+circle(5.7,6,1)+circle(10.3,6,1)+line('M4.8 9.3Q8 13 11.2 9.3'),
 person:circle(8,3,2)+'<path d="M5.5 6H10.5L12 10H10V15H6V10H4Z" fill="white"/>',
 medicine:'<path d="M1 5H15V13H1ZM4 2H12V5H10.5V3.5H5.5V5H4Z" fill="white"/>'+ '<path d="M7 6V8H5V10H7V12H9V10H11V8H9V6Z" fill="#000"/>',
 heat:line('M6.5 2V9.5A3.3 3.3 0 1 0 10 9.5V2A1.75 1.75 0 0 0 6.5 2M8.25 6V12')+line('M2 4Q4 5 2 7M13 4Q15 5 13 7'),
 cold:line('M8 1V15M2 4L14 12M2 12L14 4M5 2L8 4L11 2M5 14L8 12L11 14'),
 food:'<path d="M2 7H14C14 12 11 14 8 14S2 12 2 7ZM3 5H13V6H3Z" fill="white"/>'+line('M5 1V3M8 1V3M11 1V3'),
 power:'<path d="M9 1L2 9H7L6 15L14 6H9Z" fill="white"/>',
 warning:'<path d="M8 1L15 14H1ZM7 5V9H9V5ZM7 11V12.5H9V11Z" fill="white" fill-rule="evenodd"/>',
 danger:'<path d="M5 1H11L15 5V11L11 15H5L1 11V5ZM7 4V9H9V4ZM7 11V13H9V11Z" fill="white" fill-rule="evenodd"/>',
 quest:'<path d="M5 1H12C14 1 15 2 15 4V5H12V12Q12 15 9 15H1V12H4V4Q4 1 5 1ZM6 4V5H10V4ZM6 7V8H10V7ZM6 10V11H9V10ZM12 3V4H13.5V3Z" fill="white" fill-rule="evenodd"/>',
 research:line('M6 1H10M7 1V6L2 13Q1 15 4 15H12Q15 15 14 13L9 6V1M4 10H12'),
 check:line('M2 8L6 12L14 3'),
 info:'<path d="M8 1A7 7 0 1 0 8 15A7 7 0 0 0 8 1ZM7 7V12H9V7ZM7 4V5.5H9V4Z" fill="white" fill-rule="evenodd"/>',
 health:'<path d="M5 1H11V5H15V11H11V15H5V11H1V5H5Z" fill="white"/>',
 animal:circle(3,5,1.8)+circle(6.5,2.8,1.8)+circle(10.5,2.8,1.8)+circle(14,5.5,1.8)+'<path d="M4 10Q8 4 12 10Q16 16 8 13Q0 16 4 10Z" fill="white"/>',
 trade:line('M2 5H14M11 2L14 5L11 8M14 11H2M5 8L2 11L5 14'),
 target:'<circle cx="8" cy="8" r="4.7" fill="none" stroke="white" stroke-width="1.5"/>'+circle(8,8,1.5)+line('M8 1V4M8 12V15M1 8H4M12 8H15'),
 envelope:line('M1.5 3.5H14.5V12.5H1.5ZM1.5 4L8 9L14.5 4'),
 chevron:line('M4 6L8 10L12 6'),
 close:line('M4 4L12 12M12 4L4 12')
};
async function main(){
 for(const dir of ['Assets/RightHud','Textures/Reframed/RightHud']) fs.mkdirSync(path.join(root,dir),{recursive:true});
 for(const [name,body] of Object.entries(glyphs)){
  // Medicine's cross is a transparent cutout, not a black colored square.
  const actual=name==='medicine'?'<defs><mask id="cross"><rect width="16" height="16" fill="white"/><path d="M7 6V8H5V10H7V12H9V10H11V8H9V6Z" fill="black"/></mask></defs><g mask="url(#cross)">'+body.split('<path d="M7 6')[0]+'</g>':body;
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16">${actual}</svg>\n`;
  fs.writeFileSync(path.join(root,'Assets/RightHud',name+'.svg'),svg);
  await sharp(Buffer.from(svg),{density:432}).resize(96,96).png().toFile(path.join(root,'Textures/Reframed/RightHud',name+'.png'));
 }
 console.log(`Rendered ${Object.keys(glyphs).length} right-HUD icons.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
