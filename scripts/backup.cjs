const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {DatabaseSync}=require('node:sqlite');
const root=path.resolve(__dirname,'..');
const data=path.resolve(process.env.HALL_DATA_DIR||path.join(root,'data'));
const uploads=path.resolve(process.env.HALL_UPLOAD_DIR||path.join(root,'uploads'));
const target=path.resolve(process.argv[2]||path.join(root,'backups',new Date().toISOString().replace(/[:.]/g,'-')));
if(fs.existsSync(target))throw Error('备份目标必须为不存在的新目录');
fs.mkdirSync(target,{recursive:true});
const db=new DatabaseSync(path.join(data,'hall.db'),{readOnly:true});
try{db.exec("VACUUM INTO '"+path.join(target,'hall.db').replace(/'/g,"''")+"'");}finally{db.close();}
if(fs.existsSync(uploads))fs.cpSync(uploads,path.join(target,'uploads'),{recursive:true});
const manifest={created:new Date().toISOString(),node:process.version,files:[]};
function scan(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,e.name);if(e.isDirectory())scan(p);else manifest.files.push({path:path.relative(target,p).replaceAll('\\','/'),sha256:crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex')});}}
scan(target);fs.writeFileSync(path.join(target,'manifest.json'),JSON.stringify(manifest,null,2));
console.log('备份完成：'+target+'。密钥不包含在备份中；恢复时生成新密钥并重新登录。');
