"use client"
import {useEffect,useState} from 'react'
import {apiRequest,ApiError} from '@/lib/api-client'
type Details={customerId:number;status:string;items:{serviceName:string}[]}
export function AdminOrderOverview({bookingId}:{bookingId:number}){
 const [value,setValue]=useState<{customer:number;services:string;payment:string;accepted:number;required:number|null}|null>(null),[error,setError]=useState('')
 useEffect(()=>{const abort=new AbortController();Promise.all([apiRequest<Details>(`orders/admin/orders/${bookingId}`,{signal:abort.signal}),apiRequest<{method:string;status:string}>(`payments/orders/${bookingId}/status`,{signal:abort.signal}).catch(e=>{if(e instanceof ApiError&&e.status===404)return null;throw e}),apiRequest<{status:string}[]>(`staff/admin/bookings/${bookingId}/assignments`,{signal:abort.signal}),apiRequest<{accepted:number;requiredStaff:number}|null>(`orders/dispatch/bookings/${bookingId}`,{signal:abort.signal})]).then(([detail,payment,assignments,dispatch])=>{if(!abort.signal.aborted)setValue({customer:detail.customerId,services:detail.items.map(i=>i.serviceName).join(', '),payment:payment?`${payment.method} · ${payment.status}`:'Đơn theo luồng cũ',accepted:assignments.filter(a=>['accepted','in_progress','done'].includes(a.status)).length,required:dispatch?.requiredStaff||null})}).catch(e=>{if(!abort.signal.aborted)setError(e.message)});return()=>abort.abort()},[bookingId])
 if(error)return <span className="text-xs text-red-700">{error}</span>
 if(!value)return <span className="text-xs">Đang tải…</span>
 return <div className="max-w-xs whitespace-normal text-xs leading-6"><p>Khách #{value.customer} · {value.services}</p><p>{value.payment}</p><p>Đã nhận: {value.accepted}{value.required!==null?`/${value.required}`:''}</p></div>
}
