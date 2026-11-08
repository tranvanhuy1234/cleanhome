"use client"
import {useEffect,useState,type ReactNode} from 'react'
import {toast} from 'sonner'
import {useCurrentUser} from '@/lib/store'
import {API_URL} from '@/lib/env'
import {accessToken,apiRequest} from '@/lib/api-client'
import {AreaLink as Link} from './area-routes'
import {Button} from '../ui/button'
type Notice={id:number;booking_id:number;code:string;status:string;note:string;created_at:string}
export function OrderNotifications({children}:{children:ReactNode}){
 const user=useCurrentUser(),[items,setItems]=useState<Notice[]>([]),[open,setOpen]=useState(false),[unread,setUnread]=useState(0)
 useEffect(()=>{setItems([]);setUnread(0);setOpen(false);if(!user||!['admin','customer'].includes(user.role))return;const abort=new AbortController(),token=accessToken(),storage='cleanhome-notices-'+user.role+'-'+user.id;let last=Number(sessionStorage.getItem(storage)||'0'),initialized=last>0
  function update(data:Notice[]){if(abort.signal.aborted||accessToken()!==token)return;setItems(data);const incoming=data.filter(item=>item.id>last);if(initialized&&incoming.length){setUnread(n=>n+incoming.length);toast.info(incoming[0].note);window.dispatchEvent(new Event('cleanhome-jobs-changed'))}last=Math.max(last,...data.map(item=>item.id));initialized=true;sessionStorage.setItem(storage,String(last))}
  async function fallback(){try{update(await apiRequest<Notice[]>('orders/notifications',{signal:abort.signal}))}catch{}}
  async function connect(){while(!abort.signal.aborted){try{const response=await fetch(API_URL.replace(/\/$/,'')+'/orders/notifications/events',{headers:{Authorization:'Bearer '+token},signal:AbortSignal.any([abort.signal,AbortSignal.timeout(60000)]),cache:'no-store'});if(response.status===401||response.status===403)break;if(!response.ok||!response.body)throw Error('Unavailable');const reader=response.body.getReader(),decoder=new TextDecoder();let buffer='';while(!abort.signal.aborted){const value=await reader.read();if(value.done||abort.signal.aborted)break;buffer+=decoder.decode(value.value,{stream:true});let index;while((index=buffer.indexOf('\n\n'))>=0){const event=buffer.slice(0,index);buffer=buffer.slice(index+2);if(event.startsWith('data: '))update(JSON.parse(event.slice(6)))}}}catch{if(abort.signal.aborted)break;await fallback()}if(!abort.signal.aborted)await new Promise<void>(resolve=>{const done=()=>{clearTimeout(timer);abort.signal.removeEventListener('abort',done);resolve()};const timer=setTimeout(done,3000);abort.signal.addEventListener('abort',done,{once:true})})}}
  void connect();const focus=()=>void fallback();window.addEventListener('focus',focus);return()=>{abort.abort();window.removeEventListener('focus',focus)}
 },[user?.id,user?.role])
 return <>{children}{user&&['admin','customer'].includes(user.role)&&<aside className="fixed bottom-4 left-4 z-[70]"><Button variant="outline" onClick={()=>{setOpen(v=>!v);setUnread(0)}}>Thông báo {unread>0?`(${unread})`:''}</Button>{open&&<section aria-label="Thông báo đơn hàng" className="mt-2 max-h-[60vh] w-[calc(100vw-2rem)] max-w-sm overflow-auto rounded-xl border bg-white p-4 shadow-xl">{!items.length&&<p>Chưa có thông báo.</p>}{items.slice(0,30).map(item=><Link key={item.id} href={`/don/${item.booking_id}`} className="my-2 block rounded-lg border p-3 text-sm" onClick={()=>setOpen(false)}><strong>{item.code}</strong><p>{item.note}</p><time>{new Date(item.created_at).toLocaleString('vi-VN')}</time></Link>)}</section>}</aside>}</>
}
