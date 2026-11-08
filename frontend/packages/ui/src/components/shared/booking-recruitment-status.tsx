"use client"
import {useEffect,useState} from 'react'
import {apiRequest} from '@/lib/api-client'
type Status={bookingId:number;state:string;requiredStaff:number;filled:number;accepted:number;round:number;offerUntil:string|null;searchUntil:string}
const labels:Record<string,string>={searching:'Đang tìm nhân viên',filled:'Đã giữ đủ vị trí',support:'Chờ admin hỗ trợ',paused:'Tạm dừng tìm người',cancelled:'Đã hủy tìm người'}
export function BookingRecruitmentStatus({bookingId}:{bookingId:number}){
 const [status,setStatus]=useState<Status|null>(null),[error,setError]=useState('')
 useEffect(()=>{const controller=new AbortController();let timer:ReturnType<typeof setTimeout>|undefined
  async function sync(){try{const data=await apiRequest<Status|null>(`orders/dispatch/bookings/${bookingId}`,{signal:controller.signal});if(controller.signal.aborted)return;setStatus(data||null);setError('')}catch(e){if(!controller.signal.aborted)setError(e instanceof Error?e.message:'Chưa đồng bộ được trạng thái tìm người.')}
   if(!controller.signal.aborted)timer=setTimeout(()=>void sync(),4000)
  }void sync();return()=>{controller.abort();if(timer)clearTimeout(timer)}
 },[bookingId])
 if(error)return <p role="status" className="my-4 rounded-lg bg-amber-50 p-4 text-sm">{error}</p>
 if(!status)return null
 if(status.state==='awaiting_payment')return <p className="my-4 rounded-xl bg-blue-50 p-4">Đơn đang chờ xác minh thanh toán VNPay. Hệ thống chỉ tìm nhân viên sau khi thanh toán thành công.</p>
 return <section className="my-4 rounded-xl border border-blue-100 bg-blue-50 p-5"><h2 className="font-semibold">{status.accepted>=status.requiredStaff?'Đã đủ nhân viên nhận việc':labels[status.state]||'Đang đồng bộ tuyển người'}</h2><p className="mt-2">Đã nhận {status.accepted}/{status.requiredStaff} · còn thiếu {Math.max(0,status.requiredStaff-status.accepted)} người.</p><p className="mt-2 text-sm">{status.state==='support'?'Các vị trí đã nhận được giữ nguyên; admin sẽ hỗ trợ tìm đủ người.':'Hệ thống gửi lời mời cho nhân viên phù hợp. Chưa đủ người thì chưa thể bắt đầu.'}</p></section>
}
