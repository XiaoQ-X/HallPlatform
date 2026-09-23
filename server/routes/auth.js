module.exports=(jwt,SECRET,auth)=>{
  const r=require('express').Router();const bcrypt=require('bcryptjs');
  const {db,fail,text,transaction,audit}=require('../common');const attempts=new Map();const presentation=require('../presentation-identity');
  const token=u=>jwt.sign({id:u.id,version:u.token_version},SECRET,{algorithm:'HS256',expiresIn:'8h'});
  r.post('/login',async(req,res,next)=>{try{
    const username=text(req.body.username,'账号',80);text(req.body.password,'密码',100);const pass=req.body.password;
    const key=req.ip+':'+username,now=Date.now();const state=attempts.get(key)||{count:0,until:now+600000};
    if(state.until<now){state.count=0;state.until=now+600000;}if(++state.count>20)fail('尝试过多，请10分钟后重试',429);attempts.set(key,state);
    if(attempts.size>10000)for(const [k,v]of attempts)if(v.until<now)attempts.delete(k);
    const u=db.prepare('SELECT * FROM users WHERE username=? AND active=1').get(username);
    if(!u||!await bcrypt.compare(pass,u.password))fail('用户名或密码错误',401);
    if(u.must_change)fail('此账户使用过公开初始密码，请教师在服务器重置后登录',403);
    attempts.delete(key);const visible=presentation.visibleProfile(db,u);res.json({token:token(u),role:u.role,name:visible.name});
  }catch(e){next(e);}});
  r.post('/register',()=>fail('学生账号由任课教师创建，请联系教师',403));
  r.get('/classes',auth,(req,res)=>res.json(req.user.role==='teacher'?db.prepare('SELECT id,name,code FROM classes WHERE teacher_id=?').all(req.user.id):db.prepare('SELECT id,name FROM classes WHERE id=?').all(req.user.class_id)));
  r.post('/logout',auth,(req,res)=>{db.prepare('UPDATE users SET token_version=token_version+1 WHERE id=?').run(req.user.id);res.json({ok:1});});
  r.put('/me',auth,(req,res)=>{
    const u=db.prepare('SELECT * FROM users WHERE id=?').get(req.user.id),b=req.body;
    const alias=presentation.enabled(db)?presentation.alias(db,u.id):null;
    const name=presentation.enabled(db)?((u.role==='student'?'同学':'教师')+(alias?.ordinal||1)):(b.name==null?u.name:text(b.name,'姓名',80)),avatar=b.avatar==null?u.avatar:text(b.avatar,'头像',30);let hash=u.password;
    if(b.newPassword){text(b.newPassword,'密码',72);if(b.newPassword.length<6)fail('密码至少6位');if(typeof b.oldPassword!=='string'||!bcrypt.compareSync(b.oldPassword,u.password))fail('原密码不正确');hash=bcrypt.hashSync(b.newPassword,12);}
    transaction(()=>{db.prepare('UPDATE users SET name=?,avatar=?,password=?,token_version=token_version+? WHERE id=?').run(name,avatar,hash,b.newPassword?1:0,u.id);audit(u.id,'profile',u.id,null,{name,passwordChanged:!!b.newPassword});});
    const fresh=db.prepare('SELECT * FROM users WHERE id=?').get(u.id);
    res.json({user:presentation.visibleProfile(db,db.prepare('SELECT id,username,name,role,class_id,student_no,avatar FROM users WHERE id=?').get(u.id)),token:token(fresh)});
  });return r;
};
