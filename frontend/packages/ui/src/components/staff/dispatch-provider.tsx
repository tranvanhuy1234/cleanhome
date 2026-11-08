"use client"
import {toast} from 'sonner'
import {createContext,useContext,useEffect,useRef,useState,type ReactNode} from 'react'
import {useCurrentUser} from '@/lib/store'
import {API_URL} from '@/lib/env'
import {accessToken,apiRequest,setAccessToken,ApiError} from '@/lib/api-client'
import {AreaLink as Link} from '../shared/area-routes'
import {Button} from '../ui/button'
export type Offer={id:number;bookingId:number;code:string;serviceName:string;area:string;start:string;end:string;amount:number;estimatedShare:number;requiredStaff:number;remaining:number;distanceKm:number|null;expiresAt:string;state:string}
type Presence={ready:boolean;latitude:number|null;longitude:number|null;accuracy:number|null;positionAt:string|null;fresh:boolean}
type Operation={id:string;state:string;errorMessage?:string}
const Context=createContext<{offers:Offer[];connected:boolean;refresh:()=>Promise<void>;accept:(offer:Offer)=>Promise<string>;skip:(id:number)=>Promise<void>}|null>(null)
export function useDispatch(){const context=useContext(Context);if(!context)throw new Error('Thiếu kết nối phân phối đơn.');return context}
const money=(amount:number)=>new Intl.NumberFormat('vi-VN',{style:'currency',currency:'VND'}).format(amount)
export function DispatchProvider({children}:{children:ReactNode}){
 const user=useCurrentUser(),[offers,setOffers]=useState<Offer[]>([]),[connected,setConnected]=useState(false),[popup,setPopup]=useState<Offer|null>(null),[sound,setSound]=useState(false),[popupBusy,setPopupBusy]=useState(false)
 const seen=useRef(new Set<number>()),keys=useRef(new Map<number,string>()),audio=useRef<AudioContext|null>(null),soundEnabled=useRef(false)
 function update(items:Offer[]){setOffers(items);setPopup(current=>current&&items.some(o=>o.id===current.id)?current:null);const fresh=items.filter(o=>!seen.current.has(o.id));for(const o of items)seen.current.add(o.id);if(fresh.length){setPopup(fresh[0]);if(soundEnabled.current&&audio.current?.state==='running'){const osc=audio.current.createOscillator(),gain=audio.current.createGain();gain.gain.value=0.08;osc.connect(gain);gain.connect(audio.current.destination);osc.frequency.value=660;osc.start();osc.stop(audio.current.currentTime+0.18)}}}
 async function refresh(){const token=accessToken();if(user?.role==='staff'&&token){const items=await apiRequest<Offer[]>('staff/me/dispatch/offers');if(accessToken()===token)update(items)}}
 useEffect(()=>{
  seen.current.clear();keys.current.clear();setOffers([]);setPopup(null);if(user?.role!=='staff')return
  const controller=new AbortController();let retryTimer:ReturnType<typeof setTimeout>|undefined
  const sync=()=>{void refresh().catch(()=>{})};window.addEventListener('focus',sync)
  async function connect(){
   while(!controller.signal.aborted){try{
    const token=accessToken();if(!token)break
    const response=await fetch(API_URL.replace(/\/$/,'')+'/staff/me/dispatch/events',{headers:{Authorization:'Bearer '+token},signal:AbortSignal.any([controller.signal,AbortSignal.timeout(60000)]),cache:'no-store'})
    if(controller.signal.aborted)break
    if(response.status===401||response.status===403){if(accessToken()===token){setAccessToken(null);window.dispatchEvent(new Event('cleanhome-session-expired'))}break}
    if(!response.ok||!response.body)throw new Error('Realtime unavailable');setConnected(true)
    const reader=response.body.getReader(),decoder=new TextDecoder();let buffer=''
    while(!controller.signal.aborted){const value=await reader.read();if(value.done||controller.signal.aborted||accessToken()!==token)break;buffer+=decoder.decode(value.value,{stream:true});let index;while((index=buffer.indexOf('\n\n'))>=0){const event=buffer.slice(0,index);buffer=buffer.slice(index+2);if(event.startsWith('data: '))update(JSON.parse(event.slice(6)) as Offer[]);else if(event.startsWith('event: jobs'))window.dispatchEvent(new Event('cleanhome-jobs-changed'))}}
   }catch{if(controller.signal.aborted)break;setConnected(false);await refresh().catch(()=>{})}
   if(!controller.signal.aborted)await new Promise<void>(resolve=>{retryTimer=setTimeout(resolve,3000);controller.signal.addEventListener('abort',()=>{if(retryTimer)clearTimeout(retryTimer);resolve()},{once:true})})
   }
  }
  void connect();return()=>{controller.abort();if(retryTimer)clearTimeout(retryTimer);window.removeEventListener('focus',sync);setConnected(false)}
 // One stream per authenticated staff session, preserved while navigating.
 // eslint-disable-next-line react-hooks/exhaustive-deps
 },[user?.id,user?.role])
 async function accept(offer:Offer){
  const key=keys.current.get(offer.id)||crypto.randomUUID();keys.current.set(offer.id,key)
  let result:Operation
  try{result=await apiRequest<Operation>(`orders/dispatch/offers/${offer.id}/accept`,{method:'POST',body:JSON.stringify({idempotencyKey:key})})}
  catch(error){if(error instanceof ApiError&&error.status>=400&&error.status<500)keys.current.delete(offer.id);throw error}
  for(let i=0;result.state==='pending'&&i<20;i++){await new Promise(r=>setTimeout(r,1500));result=await apiRequest<Operation>('orders/operations/'+result.id)}
  if(result.state==='failed'){keys.current.delete(offer.id);throw new Error(result.errorMessage||'Chưa nhận được đơn.')}
  await refresh();window.dispatchEvent(new Event('cleanhome-jobs-changed'));if(result.state==='succeeded')keys.current.delete(offer.id)
  return result.state==='pending'?'Yêu cầu đang được máy chủ xử lý. Hãy đồng bộ công việc trước khi thử lại.':'Đã nhận việc thành công.'
 }
 async function skip(id:number){await apiRequest(`staff/me/dispatch/offers/${id}/skip`,{method:'POST'});await refresh()}
 async function enableSound(){try{audio.current??=new AudioContext();await audio.current.resume();soundEnabled.current=!sound;setSound(!sound)}catch{setSound(false)}}
 return <Context.Provider value={{offers,connected,refresh,accept,skip}}>{children}{user?.role==='staff'&&popup&&<aside role="status" className="fixed right-4 bottom-4 z-[80] w-[calc(100vw-2rem)] max-w-sm rounded-2xl border bg-white p-5 shadow-xl"><p className="text-sm font-semibold text-primary">Có lời mời nhận đơn mới</p><p className="mt-2 font-semibold">{popup.serviceName}</p><p className="my-2 text-sm">{popup.area} · {money(popup.amount)} · còn {popup.remaining}/{popup.requiredStaff} vị trí</p><div className="flex flex-wrap gap-2"><Button disabled={popupBusy} onClick={()=>{setPopupBusy(true);void accept(popup).then(message=>{toast.info(message);setPopup(null)}).catch(error=>toast.error(error.message)).finally(()=>setPopupBusy(false))}}>{popupBusy?'Đang nhận…':'Nhận đơn'}</Button><Button render={<Link href="/cong-viec"/>} nativeButton={false} onClick={()=>setPopup(null)}>Xem đơn mới</Button><Button variant="outline" onClick={()=>setPopup(null)}>Đóng</Button><Button variant="ghost" onClick={()=>void enableSound()}>{sound?'Tắt âm báo':'Bật âm báo'}</Button></div></aside>}</Context.Provider>
}
export function StaffReadiness(){
 const {connected,offers,refresh}=useDispatch(),[presence,setPresence]=useState<Presence|null>(null),[busy,setBusy]=useState(false),[error,setError]=useState(''),[fallback,setFallback]=useState(false)
 const intent=useRef(0),lock=useRef(false)
 useEffect(()=>{let live=true;apiRequest<Presence>('staff/me/dispatch/presence').then(p=>{if(live)setPresence(p)}).catch(e=>{if(live)setError(e.message)});return()=>{live=false;intent.current++}},[])
 useEffect(()=>{if(!presence?.ready)return;let live=true;const timer=setInterval(()=>{void apiRequest<Presence>('staff/me/dispatch/presence').then(value=>{if(live)setPresence(value)}).catch(()=>{})},15000);return()=>{live=false;clearInterval(timer)}},[presence?.ready])
 async function save(body:object){const p=await apiRequest<Presence>('staff/me/dispatch/presence',{method:'PUT',body:JSON.stringify(body)});setPresence(p);await refresh()}
 function gps(){if(lock.current)return;lock.current=true;setBusy(true);setError('');setFallback(false);const current=++intent.current
  if(!navigator.geolocation){setError('Trình duyệt chưa có định vị.');setFallback(true);setBusy(false);lock.current=false;return}
  navigator.geolocation.getCurrentPosition(position=>{if(current!==intent.current)return;void save({ready:true,latitude:position.coords.latitude,longitude:position.coords.longitude,accuracy:position.coords.accuracy,observedAt:new Date(position.timestamp).toISOString()}).catch(e=>{setError(e.message);setFallback(true)}).finally(()=>{setBusy(false);lock.current=false})},()=>{if(current!==intent.current)return;setError('Chưa được cấp GPS hoặc không lấy được vị trí. Không có theo dõi chạy nền.');setFallback(true);setBusy(false);lock.current=false},{enableHighAccuracy:true,maximumAge:30000,timeout:15000})
 }
 async function off(){intent.current++;setBusy(true);setError('');try{await save({ready:false});setFallback(false)}catch(e){setError(e instanceof Error?e.message:'Chưa lưu được trạng thái.')}finally{setBusy(false);lock.current=false}}
 return <section className="mb-6 rounded-2xl border bg-white p-5"><div className="flex flex-wrap items-center justify-between gap-3"><div><h2 className="font-semibold">Sẵn sàng nhận việc</h2><p className="mt-1 text-sm text-muted-foreground">{presence?.ready?'Đang bật':'Đang tắt'} · {connected?'Đang kết nối thông báo':'Đang đồng bộ lại thông báo'} · {offers.length} đơn mới</p></div><div className="flex gap-2"><Button role="switch" aria-checked={!!presence?.ready} disabled={busy} variant={presence?.ready?'outline':'default'} onClick={()=>presence?.ready?void off():gps()}>{busy?'Đang cập nhật…':presence?.ready?'Tắt sẵn sàng':'Bật sẵn sàng & GPS'}</Button>{presence?.ready&&<Button variant="outline" disabled={busy} onClick={gps}>Cập nhật vị trí</Button>}</div></div><p className="mt-3 text-sm text-muted-foreground">GPS: {presence?.positionAt?`${presence.fresh?'Còn mới':'Cần cập nhật'} · ${new Date(presence.positionAt).toLocaleString('vi-VN')} · sai số ${Math.round(presence.accuracy||0)} m`:'Chưa có vị trí'}. Chỉ lấy vị trí khi bạn bấm nút, không theo dõi liên tục.</p>{error&&<p role="alert" className="mt-2 text-sm text-red-700">{error}</p>}{fallback&&<Button className="mt-3" variant="outline" disabled={busy} onClick={()=>{setBusy(true);void save({ready:true}).catch(e=>setError(e.message)).finally(()=>setBusy(false))}}>Sẵn sàng theo khu vực chi nhánh nếu được phép</Button>}</section>
}
