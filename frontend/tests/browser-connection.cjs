// Requires a dedicated headless Chrome instance at port 9227 and the three sites.
// Read-only smoke test: only submits invalid credentials; creates no accounts/orders.
const assert=require('node:assert/strict')
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms))
async function main(){
 const targets=await(await fetch('http://127.0.0.1:9227/json')).json()
 const target=targets.find(t=>t.type==='page')
 const socket=new WebSocket(target.webSocketDebuggerUrl)
 await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject})
 let sequence=0
 const pending=new Map(),responses=[]
 socket.onmessage=event=>{const data=JSON.parse(event.data);if(data.id){const task=pending.get(data.id);if(task){pending.delete(data.id);data.error?task.reject(new Error(data.error.message)):task.resolve(data.result)}}else if(data.method==='Network.responseReceived')responses.push(data.params.response)}
 const call=(method,params={})=>new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params}))})
 const evaluate=async expression=>{const result=await call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(result.exceptionDetails)throw new Error(result.exceptionDetails.text);return result.result.value}
 async function until(expression){for(let i=0;i<60;i++){if(await evaluate(expression))return;await delay(500)}throw new Error('Browser condition timed out: '+expression)}
 try{
  await call('Network.enable')
  for(const [port,role] of [[3001,'customer'],[3002,'staff'],[3003,'admin']]){
   responses.length=0
   await call('Page.navigate',{url:`http://localhost:${port}/dang-nhap`})
   await until('!!document.querySelector("input[type=email]")')
   await delay(1500)
   await evaluate(`(()=>{const set=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;const email=document.querySelector('input[type=email]');const password=document.querySelector('input[type=password]');set.call(email,'frontend-smoke-nonexistent@example.test');email.dispatchEvent(new Event('input',{bubbles:true}));set.call(password,'InvalidTestPassword123');password.dispatchEvent(new Event('input',{bubbles:true}));})()`)
   await delay(100)
   await evaluate(`document.querySelector('input[type=email]').closest('form').requestSubmit()`)
   await until(`document.body.innerText.includes('Tài khoản hoặc mật khẩu không chính xác')`)
   assert.ok(responses.some(r=>r.url.includes('/api/auth/login')&&r.status===401),role+' must use actual auth API')
   assert.ok(responses.some(r=>r.url.includes('/api/catalog/services')&&r.status===200),role+' must load actual catalog')
   assert.equal(await evaluate(`Object.keys(localStorage).some(k=>k.startsWith('cleanhome-demo-state-'))`),false)
   assert.equal(await evaluate(`document.body.innerText.includes('Tài khoản trải nghiệm')`),false)
   console.log(`PASS: ${role} renders, loads real gateway catalog, rejects invalid login through backend, preserves empty demo storage.`)
  }
 }finally{await call('Browser.close').catch(()=>{});socket.close()}
}
main().catch(e=>{console.error(e.message);process.exitCode=1})
