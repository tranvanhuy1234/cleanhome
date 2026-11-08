const assert = require('node:assert/strict')
const fs = require('node:fs')
const path = require('node:path')
const vm = require('node:vm')
const ts = require('typescript')

async function scenario(role, run) {
  const storage = new Map(), handlers = new Map(), modules = new Map(), requests = []
  let respond = () => new Response('{}')
  const window = {
    sessionStorage: {getItem:k=>storage.get(k)??null,setItem:(k,v)=>storage.set(k,v),removeItem:k=>storage.delete(k)},
    addEventListener:(name,fn)=>handlers.set(name,fn),
    dispatchEvent:event=>handlers.get(event.type)?.(event),
  }
  function load(file) {
    file=path.resolve(file)
    if(modules.has(file))return modules.get(file).exports
    const module={exports:{}};modules.set(file,module)
    const code=ts.transpileModule(fs.readFileSync(file,'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2020}}).outputText
    const context={module,exports:module.exports,require:name=>load(path.join(path.dirname(file),name+'.ts')),process:{env:{NEXT_PUBLIC_APP_ROLE:role}},window,Headers,AbortSignal,Event,URLSearchParams,fetch:async(url,init)=>{requests.push({url,init});return respond(url,init)}}
    vm.runInNewContext(code,context,{filename:file});return module.exports
  }
  const api=load(path.join(__dirname,'../packages/core/src/api-client.ts')),store=load(path.join(__dirname,'../packages/core/src/backend-store.ts'))
  const directions=load(path.join(__dirname,'../packages/core/src/directions.ts'))
  await run({api,store,directions,requests,storage,respond:fn=>respond=fn})
}
const json=(data,status=200)=>new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json'}})
async function main(){
 await scenario('customer',async({api,store,respond,requests,storage})=>{
  respond((url,init)=>{
   if(url.includes('catalog/services'))return json({items:[{id:7,categoryId:3,categoryName:'Dịch vụ chuyên sâu',name:'Dịch vụ thật',description:'Mô tả',basePrice:150000,unit:'giờ',durationMinutes:60}],total:1})
   if(url.endsWith('auth/login'))return json({id:8,email:'customer@example.test',role:'customer',accessToken:'test-only-token'})
   if(url.endsWith('auth/session'))return json({id:8,email:'customer@example.test',role:'customer'})
   if(url.endsWith('users/me')&&init.method==='PUT')return json({})
   if(url.endsWith('users/me'))return json({fullName:'Khách thử nghiệm',phone:'0901234567',createdAt:'2026-01-01T00:00:00Z'})
   if(url.endsWith('auth/logout'))return new Response(null,{status:204})
   throw new Error('Unexpected URL '+url)
  })
  store.subscribeBackend(()=>{})
  await store.refreshBackend()
  assert.equal(store.backendSnapshot().services[0].categoryName,'Dịch vụ chuyên sâu')
  assert.equal(store.backendSnapshot().users.length,0)
  const login=await store.backendLogin('customer@example.test','not-a-real-password')
  assert.equal(login.ok,true);assert.equal(store.backendSnapshot().session.userId,'8')
  assert.equal(store.backendSnapshot().users[0].name,'Khách thử nghiệm')
  assert.equal(requests.find(r=>r.url.endsWith('auth/session')).init.headers.get('Authorization'),'Bearer test-only-token')
  assert.ok([...storage.values()].every(v=>!v.includes('not-a-real-password')))
  assert.equal((await store.backendSaveProfile('9',{name:'Other',email:'customer@example.test',phone:'0901234567',address:''})).ok,false)
  assert.equal((await store.backendSaveProfile('8',{name:'Đổi tên',email:'customer@example.test',phone:'0901234567',address:''})).ok,true)
  respond(()=>json({title:'Phiên hết hạn'},401))
  await assert.rejects(api.apiRequest('orders/me'),e=>e.status===401)
  assert.equal(api.accessToken(),null);assert.equal(store.backendSnapshot().session,null)
 })
 await scenario('staff',async({store,respond,requests})=>{
  respond((url)=>url.endsWith('auth/login')?json({id:1,email:'a@example.test',role:'admin',accessToken:'wrong-role-token'}):new Response(null,{status:204}))
  const result=await store.backendLogin('a@example.test','test-password')
  assert.equal(result.ok,false);assert.equal(store.backendSnapshot().session,null)
  assert.ok(requests.some(r=>r.url.endsWith('auth/logout')))
 })
 await scenario('customer',async({api,respond})=>{
  respond(()=>json({errors:{phone:['Điện thoại không hợp lệ.']}},400))
  await assert.rejects(api.apiRequest('users/me'),/Điện thoại không hợp lệ/)
  respond(()=>{throw new Error('network')})
  await assert.rejects(api.apiRequest('catalog/services'),e=>e.status===0&&e.message.includes('Không kết nối'))
 })
 await scenario('staff',async({directions})=>{
  assert.equal(directions.estimatedDistanceKm({latitude:0,longitude:0},{latitude:0,longitude:1}),111.2)
  assert.equal(directions.estimatedDistanceKm({latitude:0,longitude:179},{latitude:0,longitude:-179}),222.4)
  assert.equal(directions.estimatedDistanceKm({latitude:null,longitude:0},{latitude:0,longitude:1}),null)
  assert.equal(directions.estimatedDistanceKm({latitude:91,longitude:0},{latitude:0,longitude:1}),null)
  const pinned=new URL(directions.directionsUrl({latitude:10.7769,longitude:106.7009,addressText:'Wrong text'}))
  assert.equal(pinned.origin,'https://www.google.com');assert.equal(pinned.searchParams.get('destination'),'10.7769,106.7009');assert.equal(pinned.searchParams.get('dir_action'),'navigate');assert.equal(pinned.searchParams.has('origin'),false)
  const fallback=new URL(directions.directionsUrl({latitude:999,longitude:106,addressText:'123 Nhà & đường #1'}))
  assert.equal(fallback.searchParams.get('destination'),'123 Nhà & đường #1')
  assert.equal(directions.directionsUrl({latitude:null,longitude:null,addressText:' '}),null)
  assert.equal(directions.directionsUrl({latitude:10,longitude:null,addressText:''}),null)
 })
 console.log('PASS: API mapping, auth/errors and Google Maps navigation URL (pin, text fallback, encoding, missing location); no demo fallback or password persistence.')
}
main().catch(e=>{console.error(e);process.exitCode=1})
