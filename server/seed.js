const db = require('./db');
const bcrypt = require('bcryptjs');

function seed() {
  const count = db.prepare('SELECT COUNT(*) c FROM users').get().c;
  if (count > 0) { console.log('种子数据已存在，跳过'); return; }
  const hash = bcrypt.hashSync('123456', 8);
  const ins = (sql, p) => db.prepare(sql).run(...p);

  // 班级
  // Public/demo fixtures deliberately use aliases. Login usernames remain
  // stable for local acceptance, while no real student identity is seeded.
  const cls = ins('INSERT INTO classes(name,code) VALUES(?,?)', ['班级1','CLASS1']);
  const classId = cls.lastInsertRowid;

  // 教师
  ins('INSERT INTO users(username,password,name,role,avatar) VALUES(?,?,?,?,?)',
    ['teacher', hash, '教师1', 'teacher', '👩‍🏫']);
  // 学生
  const students = [
    ['student','同学1','🧑‍🎓'], ['li','同学2','👨‍🎓'], ['han','同学3','👩‍🎓'],
    ['wang','同学4','🧑'], ['zhao','同学5','👩'], ['chen','同学6','👨']
  ];
  students.forEach(([u,n,a],i)=>{
    ins('INSERT INTO users(username,password,name,role,class_id,student_no,avatar) VALUES(?,?,?,?,?,?,?)',
      [u, hash, n, 'student', classId, String(i + 1), a]);
  });

  // ========== 应用案例库 ==========
  const cases = [
    ['手机里的“隐形开关”——霍尔接近传感器','智能手机翻盖息屏、皮套唤醒、触控笔吸附背后的霍尔开关',
      '📱','#0EA5E9','消费电子','入门','手机,传感器,开关',
      '<h3>情境导入</h3><p>合上手机皮套，屏幕自动熄灭；翻开，又自动点亮。这不是魔法，而是皮套上的小磁体与机身内的<b>霍尔开关</b>在配合。</p><h3>工作原理</h3><p>霍尔开关内部集成霍尔元件、放大器和施密特触发器。当磁体靠近，霍尔电压超过阈值，输出低电平，通知主控“合上了”；磁体离开则输出高电平。</p><h3>拓展思考</h3><ul><li>为什么用“开关量”而不是连续电压？</li><li>霍尔开关相比机械触点有哪些寿命优势？</li></ul>',
      JSON.stringify({material:'n-silicon',thickness_mm:0.5,maxIs_mA:10,maxIm_A:1})],
    ['电动车的“电流哨兵”——霍尔电流传感器','新能源汽车、充电桩如何非接触地测量大电流',
      '🚗','#10B981','新能源','进阶','电流检测,开环,闭环',
      '<h3>情境导入</h3><p>动力电池输出数百安培电流，直接串接仪表既危险又笨重。工程师在母线上套一个霍尔电流传感器即可“隔空测流”。</p><h3>原理</h3><p>被测电流穿过磁环产生磁场，霍尔元件检测气隙磁场，输出与电流成正比的电压。闭环式还增加补偿线圈，精度更高。</p><h3>平台联动</h3><p>在仿真中固定励磁电流、改变工作电流，观察霍尔电压随电流线性变化——这正是电流传感器的标定曲线。</p>',
      JSON.stringify({material:'n-silicon',thickness_mm:0.5,maxIs_mA:10,maxIm_A:1})],
    ['让电机“知道”转子在哪——无刷电机换相','无人机、电动车、空调风机中的霍尔位置传感器',
      '🚁','#F59E0B','智能制造','进阶','BLDC,位置,换相',
      '<h3>情境导入</h3><p>无刷直流电机没有电刷，怎么知道何时切换线圈电流？答案是三颗按120°分布的霍尔元件。</p><h3>原理</h3><p>转子磁钢旋转时，霍尔元件输出三路方波，控制器据此判断转子位置并精确换相，运转平稳高效。</p><h3>讨论</h3><ul><li>为什么通常使用三颗霍尔元件而不是一颗？</li></ul>',
      JSON.stringify({material:'n-silicon',thickness_mm:0.5,maxIs_mA:10,maxIm_A:1})],
    ['管道里的“流量侦探”——电磁流量计','自来水、污水、化工浆液的无阻碍计量',
      '🚰','#6366F1','工业测量','拓展','导电液体,法拉第定律',
      '<h3>情境导入</h3><p>输水管道里没有任何转动部件，流量却被精确读出。电磁流量计基于与霍尔效应同源的洛伦兹力原理。</p><h3>原理</h3><p>励磁线圈给管道施加磁场，导电液体流动时电荷偏转，在两电极间形成与流速成正比的感应电动势 $E=B\\,D\\,v$。</p><h3>对比</h3><p>霍尔效应测的是固体中载流子偏转，电磁流量计测的是液体中电荷偏转，物理图像一脉相承。</p>',
      null],
    ['从实验室到国际标准——量子霍尔效应','1985年诺贝尔物理学奖与电阻自然基准',
      '🏅','#EC4899','前沿物理','挑战','量子化,冯·克利青,电阻基准',
      '<h3>科学里程碑</h3><p>1980年冯·克利青发现，强磁场、极低温下二维电子气的霍尔电阻呈量子化平台：$R_{xy}=\\dfrac{h}{\\nu e^2}$，精度可达10⁻¹⁰。</p><h3>意义</h3><p>该电阻只依赖基本物理常数，1990年起被用作国际电阻基准，使“欧姆”不再依赖实物标准器。</p><h3>衔接</h3><p>本平台研究的经典霍尔效应 $R_H=\\dfrac{V_H d}{I_S B}$，正是理解量子霍尔效应的起点。</p>',
      null]
  ];
  const caseCfg = [
    { material:'n-silicon', thickness_mm:0.5, maxIs_mA:10, maxIm_A:1, taskTitle:'开关特性观察', xAxis:'IM_A',
      taskGoal:'缓慢增大励磁电流 IM（模拟磁体靠近），观察霍尔电压由小到大、最终达到“开”阈值的过程，记录发生阶跃时的磁场。' },
    { material:'n-silicon', thickness_mm:0.5, maxIs_mA:10, maxIm_A:1, taskTitle:'电流传感器标定', xAxis:'IM_A',
      taskGoal:'固定工作电流 IS，在 0–1 A 范围扫描励磁 IM；在磁芯未饱和区近似拟合 VH-IM，并检查接近饱和时的曲线偏离。' },
    { material:'n-silicon', thickness_mm:0.5, maxIs_mA:10, maxIm_A:1, taskTitle:'换相方波时序', xAxis:'IM_A',
      taskGoal:'通过四方向换向（模拟转子磁钢转过），观察霍尔输出在正负磁场间切换的方波时序，理解控制器如何据此换相。' },
    { material:'n-silicon', thickness_mm:1.0, maxIs_mA:20, maxIm_A:3, taskTitle:'运动电动势资料对比', xAxis:'IM_A',
      taskGoal:'资料拓展：电磁流量计满足 E=B·D·v；本平台仅扫描仿真磁场等效量 IM，在流速 v 与电极间距 D 固定时观察 E 对 B 的正比关系，不把改变磁场误认为改变流速。' },
    { material:'gaas-2deg', thickness_mm:0.1, maxIs_mA:5, maxIm_A:2, taskTitle:'强磁场霍尔测量', xAxis:'IM_A',
      taskGoal:'在强磁场设定下学习量子霍尔电阻平台 $R_{xy}=\\frac{h}{\\nu e^2}=\\frac{R_K}{\\nu}$（其中 $R_K=\\frac{h}{e^2}$），并与经典霍尔系数 $R_H=\\frac{V_H d}{I_S B}$ 对比。' }
  ];
  cases.forEach((c,i)=>{ c[8]=JSON.stringify(caseCfg[i]); });
  cases.forEach((c,i)=>ins('INSERT INTO cases(title,subtitle,cover,color,category,difficulty,tags,content,sim_config,sort) VALUES(?,?,?,?,?,?,?,?,?,?)',
    [...c,i]));

  // ========== 思政素材库 ==========
  const ideo = [
    ['霍尔：一次“失败”实验的伟大发现','1879年，24岁的博士生霍尔对“导体在磁场中受力的到底是导体还是电荷”穷追不舍',
      '🔬','#0284C7','科学精神','物理学史',
      '<p>19世纪，麦克斯韦曾认为磁场作用于导体，而霍尔通过精巧实验证明磁场作用于其中运动的电荷。他经历无数次失败，最终在金箔上观察到微弱的横向电压。</p><h3>精神内核</h3><p>不迷信权威、敢于质疑教科书，是科学突破的起点。今天我们测量的毫伏级信号，凝聚着一位青年学者的执着。</p>'],
    ['冯·克利青：在精确测量中邂逅量子','“有时，最伟大的发现来自对细节的较真”',
      '❄️','#7C3AED','科学方法','诺贝尔奖',
      '<p>量子霍尔效应是在夜间、强磁场实验室中被意外发现的。冯·克利青注意到霍尔电阻平台竟与材料无关、只由常数决定。</p><h3>启示</h3><p>严谨的数据记录、对异常平台的敏感，让“误差”变成了诺贝尔奖。这提醒同学们：<b>善待实验中的每一个“不对劲”</b>。</p>'],
    ['黄昆：中国半导体物理的奠基人','“我愿一辈子甘当人梯”',
      '📚','#059669','家国情怀','科学家传记',
      '<p>黄昆院士是中国半导体物理和固体物理的奠基人之一，“黄昆方程”“黄-里斯理论”享誉世界。他放弃国外优渥条件回国任教，培养了新中国几代半导体人才。</p><h3>精神内核</h3><p>爱国、严谨、甘当人梯。国家最高科学技术奖得主的名字，应与我们手中的半导体样品一同被铭记。</p>'],
    ['薛其坤团队：量子反常霍尔效应的中国突破','2013年，中国科学家首次实验发现“诺贝尔奖级”成果',
      '🌟','#DC2626','科技自立','前沿突破',
      '<p>薛其坤院士带领清华大学与中科院团队，历时四年、测试上千个样品，首次在实验上观测到量子反常霍尔效应，被杨振宁称为“诺贝尔奖级的成果”。</p><h3>启示</h3><p>这一突破证明：在凝聚态物理前沿，中国科学家有能力从“跟跑”走向“领跑”。奇迹背后是上千个日夜的团队协作。</p>'],
    ['从“卡脖子”到自主可控：芯片上的中国心','霍尔元件背后是整个半导体产业的竞争',
      '💾','#EA580C','使命担当','产业报国',
      '<p>从霍尔传感器到高端芯片，半导体制造曾长期被“卡脖子”。近年来我国在材料、设备、设计领域持续突围。</p><h3>青年责任</h3><p>今天在仿真平台上理解霍尔效应的你，明天可能就是突破关键技术的人。把个人理想融入国家需要，是最深沉的“课程思政”。</p>'],
    ['王守武：为中国微电子“拓荒”','中国第一台半导体激光器、第一条集成电路生产线背后',
      '🏭','#0891B2','家国情怀','科学家传记',
      '<p>王守武院士主持建成我国第一条集成电路生产线，为微电子产业奠基。他常说“做工程要对国家负责”。</p><h3>精神内核</h3><p>从无到有的拓荒精神、理论联系实际的工程素养，正是新工科人才的底色。</p>']
  ];
  ideo.forEach((x,i)=>ins('INSERT INTO ideology(title,subtitle,cover,color,category,source,content,sort) VALUES(?,?,?,?,?,?,?,?)',
    [...x,i]));

  // ========== 题库 ==========
  const Q = (type,stem,options,answer,scoreOrAnalysis,tagsOrScore,tag) => {
    const analysis=tag?scoreOrAnalysis:'请结合课程原理核对各选项。';const score=Number(tag?tagsOrScore:scoreOrAnalysis),tags=tag||tagsOrScore;
    ins('INSERT INTO questions(type,stem,options,answer,analysis,score,tags) VALUES(?,?,?,?,?,?,?)',
      [type,stem,options?JSON.stringify(options):null,answer,analysis,score,tags]);
  };
  Q('single','霍尔电压的方向与下列哪一项无关？',
    ['工作电流方向','磁场方向','载流子带电符号','样品的长度'],'D','2','基础') ;
  Q('single','本实验中霍尔系数 R_H 的正确表达式是（d 为样品厚度）：',
    ['R_H = V_H·d/(I_S·B)','R_H = V_H·I_S/(B·d)','R_H = I_S·B/(V_H·d)','R_H = V_H·B/(I_S·d)'],
    'A','2','公式');
  Q('single','N 型半导体中参与导电的主要载流子是：',
    ['空穴','电子','正离子','光子'],'B','2','基础');
  Q('single','霍尔效应实验采用对称测量法（四方向换向）主要是为了：',
    ['让数据更多','消除不等位电势差等副效应','提高磁场','节省时间'],'B','2','方法');
  Q('single','在其他条件不变时，霍尔电压 V_H 与工作电流 I_S 的关系是：',
    ['成正比','成反比','平方关系','无关'],'A','2','规律');
  Q('single','电压表量程为 ±200 mV，屏幕显示 OL 表示：',
    ['读数为零','电压超量程','未开机','需要换向'],'B','2','仪器');
  Q('single','霍尔元件能“非接触”测量电流，主要利用了：',
    ['电流的热效应','电流产生的磁场与霍尔效应','静电感应','压电效应'],'B','2','应用');
  Q('single','量子霍尔效应中第 ν 个平台的霍尔电阻为 R_xy = h/(νe²) = R_K/ν（R_K=h/e²），其显著特点是：',
    ['随材料变化','只依赖基本物理常数','随温度变化','与磁场无关'],'B','2','前沿');
  Q('multiple','下列属于霍尔效应副效应的有：',
    ['不等位电势差','爱廷豪森效应','能斯特效应','里纪-勒杜克效应'],'ABCD','3','副效应');
  Q('multiple','开机（POWER）前必须满足的条件包括：',
    ['工作电流设定归零','励磁电流设定归零','回路连接有效','磁场调到最大'],'ABC','3','安全');
  Q('multiple','霍尔传感器常见应用场景有：',
    ['手机皮套唤醒','无刷电机换相','电流检测','电磁流量计'],'ABCD','3','应用');
  Q('multiple','下列操作会被仿真系统拒绝的有：',
    ['带电插拔导线','带电切换刀闸','读数未稳定就记录','断电后改线'],'ABC','3','安全');
  Q('judge','霍尔效应的本质是运动电荷在磁场中受洛伦兹力而横向偏转。',
    null,'对','洛伦兹力导致电荷在样品两侧积累形成霍尔电压。','2','基础');
  Q('judge','霍尔电压与样品厚度 d 成正比，因此样品越厚越容易测量。',
    null,'错','由 V_H=R_H I_S B/d，厚度越大霍尔电压反而越小。','2','规律');
  Q('judge','实验中可以带电改接导线以提高效率。',
    null,'错','带电改线违反安全规范，系统会拦截。','2','安全');
  Q('judge','量子反常霍尔效应由中国科学家薛其坤团队首次实验发现。',
    null,'对','2013年首次观测，是中国凝聚态物理的重大突破。','2','前沿');
  Q('judge','四个方向的测量顺序必须严格固定为 V1→V2→V3→V4。',
    null,'错','方向记录顺序不限，但组内电流条件必须一致。','2','方法');
  Q('fill','霍尔效应中，载流子浓度 n 与霍尔系数的关系为 n = ____（用 e 与 R_H 表示）。',
    null,'1/(e|R_H|)','由 R_H=1/(ne)（单种载流子、绝对值）。','2','公式');
  Q('fill','对称测量法合成霍尔电压的公式为 V_H = (V1−V2+V3−____)/4。',
    null,'V4','四方向对称合成。','2','公式');
  Q('fill','本平台样品固定为 ____ 型硅，厚度 0.5 mm。',
    null,'N','固定 N 型硅样品。','2','基础');

  // 试卷
  const qids = (where)=> JSON.stringify(db.prepare('SELECT id FROM questions WHERE tags LIKE ?').all(where).map(q=>q.id));
  const q1 = ins('INSERT INTO quizzes(title,description,time_minutes,question_ids) VALUES(?,?,?,?)',
    ['霍尔效应基础自测（A卷）','覆盖原理、公式、仪器与安全操作，建议20分钟内完成。',20,
      JSON.stringify(db.prepare('SELECT id FROM questions').all().slice(0,12).map(q=>q.id))]);
  ins('INSERT INTO quizzes(title,description,time_minutes,question_ids) VALUES(?,?,?,?)',
    ['应用与前沿拓展（B卷）','聚焦霍尔传感器应用、副效应与量子霍尔效应。',15,
      JSON.stringify(db.prepare('SELECT id FROM questions').all().slice(12).map(q=>q.id))]);

  // ========== 自主探究课题包 ==========
  const projects = [
    ['门磁报警器设计','用霍尔开关+蜂鸣器制作门窗入侵报警装置',
      '🚪','#0EA5E9','工程设计',
      '<p>理解霍尔开关输出特性，设计电路：磁体离开（门窗被打开）时触发报警。</p>',
      JSON.stringify([
        {t:'查阅霍尔开关（如AH3144）数据手册，明确引脚与触发方式',r:true},
        {t:'在仿真平台完成霍尔效应原理实验，记录VH-IS曲线',r:true},
        {t:'画出报警电路原理图并说明工作逻辑',r:true},
        {t:'（可选）搭建实物并录制演示视频',r:false}]),
      JSON.stringify([{n:'任务单.pdf'},{n:'AH3144数据手册.pdf'}])],
    ['霍尔电压定量关系探究','探究 V_H 与 I_S、I_M 的关系及线性近似适用范围',
      '📈','#10B981','数据探究',
      '<p>固定励磁电流扫描工作电流，再固定工作电流扫描励磁电流；在未饱和工作区进行线性拟合，并观察磁芯接近饱和时的偏离。</p>',
      JSON.stringify([
        {t:'完成 VH-IS 扫描（至少6个设定点，每点四方向测量）',r:true},
        {t:'完成 VH-IM 扫描（至少6个设定点）',r:true},
        {t:'在数据工坊拟合模型，报告斜率、R²，并说明 VH-IM 线性近似的适用范围',r:true},
        {t:'由斜率讨论霍尔灵敏度的物理意义',r:true}]),
      null],
    ['霍尔系数法测载流子浓度','综合运用 R_H、σ 求 n 与迁移率 μ',
      '🧪','#F59E0B','综合测量',
      '<p>由霍尔系数求载流子浓度 n，结合电导率 σ 求迁移率 $\\mu=|R_H|\\sigma$。</p>',
      JSON.stringify([
        {t:'测量并合成多组霍尔电压',r:true},
        {t:'计算霍尔系数 R_H 与载流子浓度 n',r:true},
        {t:'测量纵向电压求电导率 σ',r:true},
        {t:'计算迁移率 μ 并与标称值比较，分析误差来源',r:true}]),
      null],
    ['非接触电流检测模块','标定一个霍尔电流传感器',
      '🔌','#6366F1','工程设计',
      '<p>建立输出电压与被测电流的标定曲线，评估线性度、灵敏度与零点漂移。</p>',
      JSON.stringify([
        {t:'设计标定方案（标准电流源+数据采集）',r:true},
        {t:'采集标定数据并线性拟合',r:true},
        {t:'评价非线性误差与灵敏度',r:true},
        {t:'撰写标定报告',r:true}]),
      null],
    ['异常数据侦探','故意制造错误并研究系统如何“抓错”',
      '🕵️','#EC4899','开放探究',
      '<p>通过错误接线、未稳定读数等操作，收集异常事件，建立“错误-成因-纠正”对照表。</p>',
      JSON.stringify([
        {t:'至少制造5类异常操作并记录系统反馈',r:true},
        {t:'在数据清洗台登记异常与成因',r:true},
        {t:'总结常见错误预防清单',r:true}]),
      null]
  ];
  projects.forEach((p,i)=>ins('INSERT INTO projects(title,subtitle,cover,color,category,guide,tasks,attachments,sort) VALUES(?,?,?,?,?,?,?,?,?)',
    [...p,i]));

  // ========== Rubric 量表 ==========
  ins('INSERT INTO rubrics(title,target_type,dimensions) VALUES(?,?,?)',
    ['霍尔仿真实验操作量表','experiment',JSON.stringify([
      {name:'接线规范与安全',max:25,desc:'断电接线、回路正确、遵守互锁'},
      {name:'仪器操作',max:20,desc:'归零开机、合理设置电流、正确换向'},
      {name:'数据质量',max:30,desc:'四方向完整、读数稳定、组内条件一致'},
      {name:'数据处理与结论',max:25,desc:'合成正确、拟合合理、结论可靠'}])]);
  ins('INSERT INTO rubrics(title,target_type,dimensions) VALUES(?,?,?)',
    ['探究课题报告量表','project',JSON.stringify([
      {name:'方案设计',max:25},{name:'实施过程与数据',max:30},
      {name:'分析深度',max:25},{name:'报告规范与创新',max:20}])]);

  // ========== 演示仿真数据（让教师看板有内容）=========
  const studentIds = db.prepare('SELECT id,name FROM users WHERE role=?').all('student');
  const rnd = (a,b)=> a + Math.random()*(b-a);
  studentIds.forEach((s,idx)=>{
    const ss = ins('INSERT INTO sim_sessions(student_id,case_id,unity_session,finished,finished_at,state) VALUES(?,?,?,?,?,?)',
      [s.id,'hall-basic','demo-'+idx, idx<5?1:0, idx<5?'2026-09-18 10:30':'', '{}']);
    const sid = ss.lastInsertRowid;
    // 测量点
    for(let g=0; g< (idx<5?6:2); g++){
      const IS = 2+g*1.2, IM=0.4;
      const B = IM*0.8;
      const VH = IS*IM*3.2 + rnd(-0.4,0.4);
      [1,2,3,4].forEach(slot=>{
        const dirs = [[1,1],[1,-1],[-1,-1],[-1,1]][slot-1];
        ins(`INSERT INTO sim_measurements(session_id,group_id,slot,is_dir,im_dir,v_dir,IS_mA,IM_A,B_T,raw_mV,VH_mV,normVH_mV,group_complete,measured_at)
          VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,datetime('now','localtime'))`,
          [sid,'g'+g,slot,dirs[0],dirs[1],1,IS,IM,B*dirs[1],rnd(-60,60),slot===4?VH:0,slot===4?VH:0,slot===4?1:0]);
      });
    }
    // 异常
    const codes = ['READING_UNSTABLE','LIVE_SWITCH_CHANGE','CIRCUIT_INCOMPLETE','VOLTAGE_OVER_RANGE'];
    const nerr = idx===5?3:(idx%3);
    for(let k=0;k<nerr;k++){
      const c = codes[(idx+k)%codes.length];
      ins('INSERT INTO sim_abnormals(session_id,code,detail,success,step) VALUES(?,?,?,?,?)',
        [sid,c,'演示异常记录',0,5]);
    }
  });

  // 推送示例
  ins('INSERT INTO pushes(teacher_id,target_type,target_id,title,message,resource_type,resource_id) VALUES(?,?,?,?,?,?,?)',
    [1,'class',classId,'本周实验任务','请在周五前完成霍尔效应仿真实验并提交VH-IS数据。','project',2]);
  studentIds.forEach(s=>{
    ins('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)',
      [s.id,'新实验任务','教师1发布了本周霍尔效应实验任务。','/sim/lab']);
  });

  db.exec('UPDATE users SET must_change=1');
  db.exec("UPDATE sim_sessions SET demo=1 WHERE unity_session LIKE 'demo-%'");
  db.exec("UPDATE classes SET teacher_id=(SELECT id FROM users WHERE role='teacher' ORDER BY id LIMIT 1) WHERE teacher_id IS NULL");
  console.log('演示数据已创建，公开初始密码账户已锁定，请使用管理员工具重置。');
}
seed();
