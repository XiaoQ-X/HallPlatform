const fs=require('fs'),path=require('path'),bcrypt=require('bcryptjs');
const work=path.resolve(__dirname,'../work');fs.mkdirSync(work,{recursive:true});
process.env.HALL_DATA_DIR=fs.mkdtempSync(path.join(work,'full-audit-'));
process.env.HALL_UPLOAD_DIR=path.join(process.env.HALL_DATA_DIR,'uploads');process.env.HALL_DEMO='1';
const app=require('../server'),db=require('../server/db');
db.prepare('UPDATE users SET password=?,must_change=0').run(bcrypt.hashSync('Audit-Local-2026!',4));
app.listen(8183,'127.0.0.1',()=>console.log('Isolated audit: http://127.0.0.1:8183\n'+process.env.HALL_DATA_DIR));
