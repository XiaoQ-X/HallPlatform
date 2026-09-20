const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),files=[];
function add(p){files.push({path:path.relative(root,p).replaceAll('\\','/'),bytes:fs.statSync(p).size,sha256:crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex')});}
function scan(p){for(const f of fs.readdirSync(p,{withFileTypes:true})){const full=path.join(p,f.name);if(f.isDirectory())scan(full);else add(full);}}
for(const directory of ['server','src','shared','public/sim','tests','scripts'])scan(path.join(root,directory));
for(const name of ['package.json','pnpm-lock.yaml','vite.config.js'])add(path.join(root,name));
const target=path.join(root,'work','release-manifest.json');fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,JSON.stringify({version:require('../package.json').version,created:new Date().toISOString(),node:process.version,unitySourceIncluded:false,files},null,2));console.log(target);
