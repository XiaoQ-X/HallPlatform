const fs=require('fs'),path=require('path'),assert=require('node:assert/strict');
const {chromium}=require('C:/Users/a19098/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
fs.mkdirSync(path.resolve(__dirname,'../work'),{recursive:true});
process.env.HALL_DATA_DIR=fs.mkdtempSync(path.resolve(__dirname,'../work/identity-'));
process.env.HALL_UPLOAD_DIR=path.join(process.env.HALL_DATA_DIR,'uploads');process.env.HALL_DEMO='1';
const app=require('../server'),db=require('../server/db');
const password='Identity-Test-2026!';db.prepare('UPDATE users SET password=?,must_change=0').run(require('bcryptjs').hashSync(password,4));
(async()=>{
 const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));
 const base='http://127.0.0.1:'+server.address().port,result={passed:[]};let browser;
 try{
  browser=await chromium.launch({channel:'msedge',headless:true});const context=await browser.newContext();
  const teacher=await context.newPage(),other=await context.newPage();
  async function login(page,username){await page.goto(base+'/login');await page.getByPlaceholder('请输入账号').fill(username);await page.getByPlaceholder('请输入密码').fill(password);await page.getByRole('button',{name:'登 录',exact:true}).click();await page.waitForURL(base+(username==='teacher'?'/teacher':'/'));}
  await login(teacher,'teacher');assert.equal(await teacher.getByRole('link',{name:'返回学生端'}).count(),0);
  for(const route of ['/','/resources/quiz','/peer/works','/sim/lab']){await teacher.goto(base+route);await teacher.waitForURL(base+'/teacher');}
  result.passed.push('教师无学生入口，直接访问学生路由返回教师看板');
  await teacher.evaluate(()=>sessionStorage.setItem('appeal_ref','old-identity'));
  await login(other,'student');await teacher.waitForURL(base+'/');await teacher.getByText('张晓明',{exact:true}).first().waitFor();
  assert.equal(await teacher.evaluate(()=>sessionStorage.getItem('appeal_ref')),null);
  assert.equal(await teacher.getByText('教师管理端',{exact:true}).count(),0);
  result.passed.push('第二标签登录学生后，原教师标签重新加载身份并清除暂存上下文');
  await teacher.goto(base+'/teacher');await teacher.waitForURL(base+'/');result.passed.push('学生不能进入教师页面');
  await login(other,'teacher');await teacher.waitForURL(base+'/teacher');await teacher.getByText('王雅琴',{exact:true}).waitFor();
  result.passed.push('第二标签登录教师后，学生标签返回教师看板');
  await other.getByText('退出登录',{exact:true}).click();await other.waitForURL(base+'/login');await teacher.waitForURL(base+'/login');
  result.passed.push('退出登录同步到另一标签');
 }catch(e){result.failure=e.stack;process.exitCode=1;}finally{if(browser)await browser.close();await new Promise(r=>server.close(r));db.close();fs.writeFileSync(path.resolve(__dirname,'../work/identity-results.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));}
})();
