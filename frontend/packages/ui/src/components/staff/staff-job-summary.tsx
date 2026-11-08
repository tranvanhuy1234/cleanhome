"use client"
import {useEffect,useState} from 'react'
import {apiRequest} from '@/lib/api-client'
import {money,date,Table,Panel,Badge} from '../../common'
import {AreaLink as Link} from '../shared/area-routes'
type Summary={bookingId:number;code:string;status:string;services:string[];amount:number;start:string;end:string;completedAt:string|null}
export function StaffJobSummaryView({bookingId}:{bookingId:number}){
 const [value,setValue]=useState<Summary|null>(null),[error,setError]=useState('')
 useEffect(()=>{const controller=new AbortController();apiRequest<Summary>(`orders/staff/orders/${bookingId}/summary`,{signal:controller.signal}).then(r=>{if(!controller.signal.aborted)setValue(r)}).catch(e=>{if(!controller.signal.aborted)setError(e.message)});return()=>controller.abort()},[bookingId])
 if(error)return <p role="status" className="text-sm">{error}</p>
 return value?<div className="my-3 space-y-2 text-sm"><p className="font-semibold">{value.code}</p><p>{value.services.join(', ')||'Dịch vụ đã lưu'}</p><p>Giá trị đơn: {money(value.amount)}</p>{value.completedAt&&<p>Hoàn thành: {date(value.completedAt)}</p>}</div>:<p role="status" className="text-sm">Đang tải dịch vụ…</p>
}
export function StaffCalendarJobs(){
 const [jobs,setJobs]=useState<{id:number;bookingId:number;status:string;scheduledStart:string;scheduledEnd:string}[]>([]),[error,setError]=useState('')
 useEffect(()=>{const controller=new AbortController();const sync=()=>{void apiRequest<typeof jobs>('staff/me/jobs',{signal:controller.signal}).then(r=>{if(!controller.signal.aborted)setJobs(r)}).catch(e=>{if(!controller.signal.aborted)setError(e.message)})};sync();window.addEventListener('cleanhome-jobs-changed',sync);window.addEventListener('focus',sync);return()=>{controller.abort();window.removeEventListener('cleanhome-jobs-changed',sync);window.removeEventListener('focus',sync)}},[])
 return <Panel title="Lịch công việc đã nhận">{error&&<p role="alert">{error}</p>}<Table headers={['Đơn / dịch vụ','Bắt đầu','Kết thúc','Trạng thái','Địa điểm']} rows={jobs.filter(j=>j.status!=='cancelled').map(job=>[<StaffJobSummaryView bookingId={job.bookingId}/>,date(job.scheduledStart),date(job.scheduledEnd),<Badge value={job.status}/>,<Link href={`/don/${job.bookingId}`} className="underline">Xem địa điểm và chỉ đường</Link>])}/></Panel>
}
