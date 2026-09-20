const db=require('./db');
function fail(message,status=400){throw Object.assign(new Error(message),{status});}
const parse=(s,d={})=>{try{return JSON.parse(s)}catch{return d}};
function text(v,label,max=10000){if(typeof v!=='string'||!v.trim()||v.length>max)fail(label+'无效');return v.trim();}
function number(v,label,min=-Infinity,max=Infinity){if(typeof v!=='number'||!Number.isFinite(v)||v<min||v>max)fail(label+'无效');return v;}
function id(v){const n=Number(v);if(!Number.isSafeInteger(n)||n<1)fail('编号无效');return n;}
function transaction(fn){db.exec('BEGIN IMMEDIATE');try{const r=fn();db.exec('COMMIT');return r;}catch(e){db.exec('ROLLBACK');throw e;}}
function audit(user,action,target,before,after){db.prepare('INSERT INTO audit_log(actor_id,action,target,before_json,after_json) VALUES(?,?,?,?,?)').run(user,action,String(target),JSON.stringify(before??null),JSON.stringify(after??null));}
function teacher(req){if(req.user.role!=='teacher')fail('仅教师可操作',403);}
function studentAccess(user,studentId){const s=db.prepare('SELECT id,class_id FROM users WHERE id=?').get(id(studentId));if(!s)fail('学生不存在',404);if(user.id===s.id)return s;if(user.role!=='teacher'||!db.prepare('SELECT id FROM classes WHERE id=? AND teacher_id=?').get(s.class_id,user.id))fail('不在授权班级内',403);return s;}
function classAccess(user,classId){teacher({user});const c=db.prepare('SELECT * FROM classes WHERE id=? AND teacher_id=?').get(id(classId),user.id);if(!c)fail('无权管理该班级',403);return c;}
function validFiles(user,files=[]){if(!Array.isArray(files)||files.length>10)fail('附件数量无效');return files.map(f=>{const file=db.prepare('SELECT id,name,size FROM files WHERE id=? AND owner_id=?').get(f.id||String(f.url||'').split('/').pop(),user.id);if(!file)fail('附件不属于当前用户',403);return {...file,url:'/api/files/'+file.id};});}
module.exports={db,fail,parse,text,number,id,transaction,audit,teacher,studentAccess,classAccess,validFiles};
