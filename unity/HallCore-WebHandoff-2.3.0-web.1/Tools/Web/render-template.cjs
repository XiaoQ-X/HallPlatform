const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(process.argv[2]||'Builds/WebGL');
const files=fs.readdirSync(path.join(root,'Build'));
const find=suffix=>{const matches=files.filter(f=>f.endsWith(suffix));if(matches.length!==1)throw Error('Expected one '+suffix);return matches[0];};
const replacements={LOADER_FILENAME:find('.loader.js'),DATA_FILENAME:find('.data.unityweb'),FRAMEWORK_FILENAME:find('.framework.js.unityweb'),CODE_FILENAME:find('.wasm.unityweb')};
let template=fs.readFileSync('Assets/WebGLTemplates/Hall/index.html','utf8');
template=template.replace(/\{\{\{\s*(\w+)\s*\}\}\}/g,(_,key)=>{if(!replacements[key])throw Error('Unknown template field '+key);return replacements[key];});
fs.writeFileSync(path.join(root,'index.html'),template);
for(const file of ['hall-host.js','handoff.html'])fs.copyFileSync(path.join('Tools/Web',file),path.join(root,file));
