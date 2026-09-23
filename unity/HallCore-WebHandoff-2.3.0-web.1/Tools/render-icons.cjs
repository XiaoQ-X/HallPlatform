const fs = require('fs');
const path = require('path');
const { Resvg } = require('./.runtime/resvg/package/resvgjs.win32-x64-msvc.node');
const source = path.resolve(__dirname, '../Assets/ThirdParty/Lucide');
const destination = path.resolve(__dirname, '../Assets/THH/Resources/Icons');
for (const name of fs.readdirSync(source).filter(name => name.endsWith('.svg'))) {
  const svg = fs.readFileSync(path.join(source, name), 'utf8').replaceAll('currentColor', '#ffffff');
  const image = new Resvg(svg, JSON.stringify({ fitTo: { mode: 'width', value: 96 } })).render();
  fs.writeFileSync(path.join(destination, name.replace('.svg', '.png')), image.asPng());
}
console.log('Rendered official Lucide assets to 96px PNG.');
