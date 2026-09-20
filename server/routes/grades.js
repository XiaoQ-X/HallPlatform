const r=require('express').Router(),{db,parse}=require('../common');
r.get('/',(req,res)=>res.json(db.prepare('SELECT * FROM grade_publications WHERE student_id=? ORDER BY id DESC').all(req.user.id).map(g=>({...g,payload:parse(g.payload)}))));module.exports=r;
