// Local integration test. Creates ONE visible booking for the seeded sample customer.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict')
const delay=ms=>new Promise(r=>setTimeout(r,ms))
async function main(){
 const accounts=JSON.parse(fs.readFileSync(path.join(__dirname,'../../backend/.local/demo-accounts.json'),'utf8'))
 const customer=accounts.find(a=>a.Role==='customer'),admin=accounts.find(a=>a.Role==='admin')
 const targets=await(await fetch('http://127.0.0.1:9227/json')).json()
 const socket=new WebSocket(targets.find(t=>t.type==='page').webSocketDebuggerUrl)
 await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject})
 let id=0;const pending=new Map(),creationRequests=[]
 socket.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);if(p){pending.delete(m.id);m.error?p.reject(Error(m.error.message)):p.resolve(m.result)}}else if(m.method==='Network.requestWillBeSent'&&m.params.request.url.endsWith('/api/orders/booking')&&m.params.request.method==='POST')creationRequests.push(m.params.request)}
 const call=(method,params={})=>new Promise((resolve,reject)=>{const n=++id;pending.set(n,{resolve,reject});socket.send(JSON.stringify({id:n,method,params}))})
 const evaluate=async expression=>{const r=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.exception?.description||r.exceptionDetails.text);return r.result.value}
 async function until(expression){for(let i=0;i<90;i++){if(await evaluate(expression))return;await delay(500)}throw Error('Timed out: '+expression)}
 try{
  await call('Network.enable');await call('Page.navigate',{url:'http://localhost:3001/dang-nhap'})
  await until('!!document.querySelector("input[type=email]")');await delay(1000)
  await evaluate(`(()=>{const set=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;for(const [type,value] of ${JSON.stringify([['email',customer.Email],['password',customer.Password]])}){const e=document.querySelector('input[type='+type+']');set.call(e,value);e.dispatchEvent(new Event('input',{bubbles:true}))}})()`)
  await delay(100);await evaluate(`document.querySelector('input[type=email]').closest('form').requestSubmit()`)
  await until(`location.pathname!='/dang-nhap'`)
  await call('Page.navigate',{url:'http://localhost:3001/dat-dich-vu'})
  await until(`document.querySelectorAll('select').length===3&&!document.querySelector('fieldset').disabled`)
  const scheduled=new Date();scheduled.setDate(scheduled.getDate()+4)
  const localDate=`${scheduled.getFullYear()}-${String(scheduled.getMonth()+1).padStart(2,'0')}-${String(scheduled.getDate()).padStart(2,'0')}T09:00`
  await evaluate(`(()=>{const set=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;for(const [type,value] of ${JSON.stringify([['number','2'],['datetime-local',localDate]])}){const e=document.querySelector('input[type='+type+']');set.call(e,value);e.dispatchEvent(new Event('input',{bubbles:true}))}const note=document.querySelector('textarea');Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(note,'Đơn kiểm thử từ giao diện đặt dịch vụ');note.dispatchEvent(new Event('input',{bubbles:true}));})()`)
  await delay(200);await evaluate(`document.querySelector('form').requestSubmit()`)
  await until(`location.pathname.startsWith('/don/')&&Number(location.pathname.split('/').pop())>0`)
  const bookingId=Number((await evaluate('location.pathname')).split('/').pop())
  assert.equal(creationRequests.length,1)
  const input=JSON.parse(creationRequests[0].postData)
  assert.ok(input.idempotencyKey);assert.equal(input.quantity,2);assert.equal(input.totalAmount,undefined)
  const loginResponse=await fetch('http://localhost:4000/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:admin.Email,password:admin.Password})})
  assert.equal(loginResponse.status,200);const login=await loginResponse.json()
  try{
   const response=await fetch(`http://localhost:4000/api/orders/admin/orders/${bookingId}`,{headers:{Authorization:'Bearer '+login.accessToken}})
   assert.equal(response.status,200);const booking=await response.json()
   assert.equal(booking.status,'pending');assert.equal(booking.items[0].quantity,2);assert.equal(booking.totalAmount,booking.items[0].unitPrice*2)
   assert.equal(booking.addressSnapshot.id,input.addressId);assert.ok(booking.addressSnapshot.recipient);assert.ok(booking.history.some(h=>h.status==='pending'))
   console.log(`PASS: customer placed booking ${bookingId} through browser; admin reads the same pending order, snapshot, server amount and history. Sample booking retained.`)
  }finally{await fetch('http://localhost:4000/api/auth/logout',{method:'POST',headers:{Authorization:'Bearer '+login.accessToken}})}
  await evaluate(`fetch('http://localhost:4000/api/auth/logout',{method:'POST',headers:{Authorization:'Bearer '+sessionStorage.getItem('cleanhome-api-session')}}).then(()=>sessionStorage.removeItem('cleanhome-api-session'))`)
 }finally{await call('Browser.close').catch(()=>{});socket.close()}
}
main().catch(e=>{console.error(e.message);process.exitCode=1})
