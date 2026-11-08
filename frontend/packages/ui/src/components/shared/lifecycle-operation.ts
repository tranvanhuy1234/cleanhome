import {apiRequest} from '@/lib/api-client'
type Operation={id:string;state:string;errorMessage?:string;result?:{withdrawalOutcome?:string}}
/** Keep the same key while recovering an unknown response; only the server commits changes. */
export async function lifecycleOperation(route:string,body:object,key:string){
 let result=await apiRequest<Operation>(route,{method:'POST',body:JSON.stringify({...body,idempotencyKey:key})})
 for(let n=0;result.state==='pending'&&n<25;n++){await new Promise(resolve=>setTimeout(resolve,1200));result=await apiRequest<Operation>('orders/operations/'+result.id)}
 if(result.state==='failed')throw new Error(result.errorMessage||'Máy chủ từ chối thao tác.')
 window.dispatchEvent(new Event('cleanhome-jobs-changed'))
 return result
}
