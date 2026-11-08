"use client"
import {useEffect,useState} from 'react'
import {apiRequest} from '@/lib/api-client'
import {Panel,Table,Btn,date} from '../../common'
import {AreaLink as Link} from '../shared/area-routes'
type Support={id:number;booking_id:number;employee_id:number;reason:string;created_at:string}
export function AdminDispatchSupport(){
 const [items,setItems]=useState<Support[]>([]),[error,setError]=useState(''),[tick,setTick]=useState(0),[busy,setBusy]=useState(false)
 useEffect(()=>{let live=true;apiRequest<Support[]>('staff/admin/dispatch/support').then(value=>{if(live)setItems(value)}).catch(e=>{if(live)setError(e.message)});return()=>{live=false}},[tick])
 return <Panel title="Yêu cầu hỗ trợ / xin rút việc">{error&&<p role="alert">{error}</p>}<Btn secondary onClick={()=>setTick(v=>v+1)}>Tải lại yêu cầu</Btn><Table headers={['Đơn','Nhân viên','Lý do','Gửi lúc','Xử lý']} rows={items.map(item=>[<Link href={`/don/${item.booking_id}`}>Đơn #{item.booking_id}</Link>,String(item.employee_id),item.reason,date(item.created_at),<Btn disabled={busy} onClick={()=>{setBusy(true);void apiRequest(`staff/admin/dispatch/support/${item.id}/resolve`,{method:'POST'}).then(()=>setTick(v=>v+1)).catch(e=>setError(e.message)).finally(()=>setBusy(false))}}>Đánh dấu đã xử lý</Btn>])}/></Panel>
}
