const fs=require('fs'),path=require('path'),crypto=require('crypto');
const db=require('../server/db');require('../server/content-migration')(db);require('../server/formula-migration')(db);require('../server/integrity-migration')(db);
const directory=process.env.HALL_UPLOAD_DIR||path.resolve(__dirname,'../uploads');fs.mkdirSync(directory,{recursive:true});
const topics={
  1:'固定励磁电流，扫描工作电流。每个扫描点完成四方向记录。拟合归一霍尔电压与工作电流；讨论截距、残差及固定条件。',
  2:'固定工作电流，扫描励磁电流。记录磁场B与霍尔电压，分别拟合VH-B及VH-IM；VH-IM 只在磁芯未饱和区近似线性，需记录接近饱和时的偏离。不可将IM直接当成磁感应强度，需保留B-IM标定依据。',
  3:'先求霍尔系数RH=VH*d/(IS*B)。单载流子近似下n=1/(e*|RH|)。迁移率mu=|RH|*sigma，sigma须由教师提供或实物测量；本Unity实验不提供纵向电压测量。',
  4:'设计电流传感器标定方案。仿真中只能固定IS并扫描IM来类比被测电流产生的磁场；实际模块的量程、供电、灵敏度、误差限须依据所选厂商数据手册和标准电流源实测，不得将此仿真读数称为模块实测标定结果。',
  5:'记录错误接线、超量程请求、未稳定记录等不同异常，整理触发条件、系统反馈、影响与纠正证据。同条件重复测量可用中位数/MAD辅助识别疑点；异常必须记录成因，不可仅因偏离拟合而删除。'
};
const common='\n实验范围：n-silicon，厚度0.5 mm，IS≤10 mA，IM≤1 A。断电换向，四方向顺序(+,+)、(+,-)、(-,-)、(-,+)，VH=(V1-V2+V3-V4)/4。实际符号应依据接线约定记录。\n数据处理：保留原始数据、清洗依据和排除记录；按固定条件分组拟合；不确定度评定列出仪器误差限的真实来源。不得将拟合R²作为物理模型正确的唯一证据。\n报告结构：目的与模型、参数与接线、原始数据、清洗记录、拟合/残差、不确定度、结论与限制、引用。\n参考：JCGM 100:2008 (GUM)，https://www.bipm.org/en/committees/jc/jcgm/publications ；量子霍尔史料 https://www.nobelprize.org/prizes/physics/1985/summary/ ；量子反常霍尔实验 DOI 10.1126/science.1234414。\n此文件为平台配套任务单，不是仪器厂家数据手册；误差限、电导率等未提供值须由任课教师确认。\n';
let count=0;
for(const p of db.prepare('SELECT * FROM projects WHERE archived=0 AND owner_id IS NOT NULL').all()){
  const files=JSON.parse(p.attachments||'[]');if(files.some(f=>f.name==='课题任务单-v1.4.txt'))continue;
  const taskText=JSON.parse(p.tasks||'[]').map((t,i)=>`${i+1}. ${t.t||t.title||''}`).join('\n');
  const content=`${p.title}\n配套任务单 v1.4 / 2026-09-19\n\n${topics[p.id]||'按教师发布的课题任务完成探究。'}\n\n任务清单\n${taskText}\n${common}`;
  const id=crypto.randomUUID(),filename=id+'.txt';fs.writeFileSync(path.join(directory,filename),content);
  db.exec('BEGIN IMMEDIATE');try{db.prepare('INSERT INTO files(id,owner_id,name,path,mime,size) VALUES(?,?,?,?,?,?)').run(id,p.owner_id,'课题任务单-v1.4.txt',filename,'text/plain',Buffer.byteLength(content));files.push({id,name:'课题任务单-v1.4.txt',url:'/api/files/'+id,size:Buffer.byteLength(content)});db.prepare('UPDATE projects SET attachments=? WHERE id=?').run(JSON.stringify(files),p.id);db.prepare('INSERT INTO audit_log(action,target,after_json) VALUES(?,?,?)').run('install-teaching-file','project:'+p.id,JSON.stringify({file_id:id}));db.exec('COMMIT');count++;}catch(e){db.exec('ROLLBACK');throw e;}
}
console.log(`已添加 ${count} 份真实任务单附件。`);db.close();
