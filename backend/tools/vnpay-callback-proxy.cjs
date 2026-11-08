// Expose only signed VNPay callbacks, never the complete gateway or internal APIs.
const http=require('node:http')
const allowed=new Set(['/api/payments/vnpay/ipn','/api/payments/vnpay/return'])
http.createServer((request,response)=>{
 const url=new URL(request.url,'http://localhost')
 if(request.method!=='GET'||!allowed.has(url.pathname)||request.url.length>8192){response.writeHead(404);response.end();return}
 const upstream=http.request({hostname:'127.0.0.1',port:4000,path:request.url,method:'GET',headers:{host:'localhost:4000',accept:'application/json'}},result=>{response.writeHead(result.statusCode,{'content-type':result.headers['content-type']||'application/json','cache-control':'no-store',...(result.headers.location?{location:result.headers.location}:{})});result.pipe(response)})
 upstream.setTimeout(15000,()=>upstream.destroy());upstream.on('error',()=>{if(!response.headersSent)response.writeHead(503,{'content-type':'application/json'});response.end('{"RspCode":"99","Message":"Retry later"}')});upstream.end()
}).listen(4090,'127.0.0.1',()=>console.log('VNPay callback proxy ready on localhost:4090'))
