const fs=require('fs');const path=require('path');const crypto=require('crypto');
const dir=process.env.HALL_DATA_DIR||path.join(__dirname,'..','data');fs.mkdirSync(dir,{recursive:true});
const file=path.join(dir,'server-secret');let secret=process.env.HALL_JWT_SECRET;
if(!secret){if(process.env.NODE_ENV==='production')throw Error('生产环境必须设置 HALL_JWT_SECRET');if(!fs.existsSync(file))fs.writeFileSync(file,crypto.randomBytes(48).toString('hex'),{mode:0o600,flag:'wx'});secret=fs.readFileSync(file,'utf8').trim();}
if(secret.length<48)throw Error('HALL_JWT_SECRET 至少48字符');module.exports={secret,dir};
