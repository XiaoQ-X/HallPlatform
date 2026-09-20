const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {DatabaseSync}=require('node:sqlite');
const [sourceArg,targetArg]=process.argv.slice(2);
if(!sourceArg||!targetArg)throw Error('用法: node scripts/restore.cjs <备份目录> <全新恢复目录>');
const source=path.resolve(sourceArg),target=path.resolve(targetArg);
if(fs.existsSync(target))throw Error('恢复目录必须不存在，禁止覆盖正在使用的数据');
const manifest=JSON.parse(fs.readFileSync(path.join(source,'manifest.json'),'utf8'));
for(const file of manifest.files){const p=path.resolve(source,file.path);if(!p.startsWith(source+path.sep))throw Error('备份路径越界');const hash=crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');if(hash!==file.sha256)throw Error('校验失败：'+file.path);}
const db=new DatabaseSync(path.join(source,'hall.db'),{readOnly:true});
try{if(db.prepare('PRAGMA integrity_check').get().integrity_check!=='ok')throw Error('数据库完整性检查失败');}finally{db.close();}
fs.mkdirSync(path.join(target,'data'),{recursive:true});fs.copyFileSync(path.join(source,'hall.db'),path.join(target,'data','hall.db'));
if(fs.existsSync(path.join(source,'uploads')))fs.cpSync(path.join(source,'uploads'),path.join(target,'uploads'),{recursive:true});
console.log('恢复完成：'+target+'。启动前设置 HALL_DATA_DIR 和 HALL_UPLOAD_DIR 指向此目录内 data 与 uploads。');
