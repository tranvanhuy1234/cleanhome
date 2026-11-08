"use client"
import {useRef,useState} from 'react'
import {apiRequest} from '@/lib/api-client'
import {Button} from '../ui/button'
export function StaffJobSupport({assignmentId}:{assignmentId:number}){
 const [open,setOpen]=useState(false),[reason,setReason]=useState(''),[message,setMessage]=useState(''),[busy,setBusy]=useState(false);const locked=useRef(false)
 async function send(kind:'arrive'|'support'){if(locked.current)return;locked.current=true;setBusy(true);setMessage('');try{await apiRequest(`staff/me/dispatch/jobs/${assignmentId}/${kind}`,{method:'POST',...(kind==='support'?{body:JSON.stringify({reason})}:{})});setMessage(kind==='arrive'?'Đã ghi nhận bạn đến nơi.':'Đã gửi yêu cầu cho admin. Công việc vẫn giữ nguyên cho tới khi được xử lý.');setOpen(false);setReason('')}catch(e){setMessage(e instanceof Error?e.message:'Chưa gửi được.')}finally{setBusy(false);locked.current=false}}
 return <div className="mt-4 space-y-3"><div className="flex flex-wrap gap-2"><Button variant="outline" disabled={busy} onClick={()=>void send('arrive')}>Xác nhận đã đến nơi</Button><Button variant="outline" disabled={busy} onClick={()=>setOpen(v=>!v)}>Yêu cầu hỗ trợ / xin rút</Button></div>{open&&<form className="space-y-3 rounded-xl bg-slate-50 p-3" onSubmit={e=>{e.preventDefault();void send('support')}}><label className="block text-sm">Mô tả sự cố hoặc lý do xin rút<textarea className="mt-2 w-full rounded-lg border p-3" required minLength={3} maxLength={2000} value={reason} onChange={e=>setReason(e.target.value)}/></label><Button type="submit" disabled={busy}>Gửi admin xử lý</Button></form>}{message&&<p role="status" className="text-sm">{message}</p>}</div>
}
