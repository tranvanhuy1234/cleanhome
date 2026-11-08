"use client"

import {AdminOrderOverview} from '../admin/admin-order-overview'
import {AdminOrderLifecycle,AdminWithdrawals} from '../admin/admin-order-lifecycle'
import {OrderPayment} from './order-payment'
import {BookingRecruitmentStatus} from './booking-recruitment-status'
import {StaffWorkBoard} from '../staff/staff-work-board'
import {StaffDirections} from '../staff/staff-directions'
import {useCallback,useEffect,useRef,useState} from 'react'
import {apiRequest,apiPages} from '@/lib/api-client'
import type {Role} from '@/lib/types'
import {Heading,Panel,Field,Btn,Table,Badge,money,date} from '../../common'
import {AreaLink as Link} from './area-routes'

type Page<T>={items:T[];page:number;pageSize:number;total:number}
type Summary={id:number;code:string;status:string;totalAmount:number;scheduledStart:string}
type Booking=Summary&{version:number;specialRequest?:string;items:{id:number;serviceName:string;quantity:number;unitPrice:number}[];history:{id:number;status:string;note:string;createdAt:string}[]}
type Assignment={id:number;bookingId:number;employeeId:number;userId:number;status:string;scheduledStart:string;scheduledEnd:string}
type Cancellation={id:number;reason:string;status:string;adminNote?:string}
type Operation={id:string;state:string;errorMessage?:string}
type Review={id:number;bookingId:number;employeeId:number;rating:number;comment:string;isHidden:boolean}
type Employee={id:number;userId:number;branchId:number;status:string;verificationStatus:string}
type Skill={id:number;name:string}
type Account={id:number;email:string;role:string;status:string}
const message=(e:unknown)=>e instanceof Error?e.message:'Không kết nối được máy chủ.'
const inputClass='min-h-11 w-full rounded-lg border p-3'

export function BackendWorkspace({role,path}:{role:Role;path:string[]}){
 const [error,setError]=useState(''),[notice,setNotice]=useState(''),[busy,setBusy]=useState(false),[tick,setTick]=useState(0)
 const locked=useRef(false)
 const [operation,setOperation]=useState<Operation|null>(null)
 const retry=useRef<{fingerprint:string;key:string}|null>(null)
 const [reviewEmployee,setReviewEmployee]=useState('')
 const [reason,setReason]=useState(''),[rating,setRating]=useState(5)
 const [summaries,setSummaries]=useState<Summary[]>([]),[booking,setBooking]=useState<Booking|null>(null),[jobs,setJobs]=useState<Assignment[]>([]),[cancellations,setCancellations]=useState<Cancellation[]>([]),[reviews,setReviews]=useState<Review[]>([]),[employees,setEmployees]=useState<Employee[]>([]),[skills,setSkills]=useState<Skill[]>([]),[accounts,setAccounts]=useState<Account[]>([])
 const [selected,setSelected]=useState<number[]>([]),[required,setRequired]=useState<number[]>([])
 const [trash,setTrash]=useState(false)
 const [page,setPage]=useState(1),[total,setTotal]=useState(0),[search,setSearch]=useState(''),[status,setStatus]=useState('')
 const route=path[0]||'',id=path[1]
 const base=role==='admin'?'orders/admin/orders':'orders/me'
 const load=useCallback(async()=>{
   setError('')
   try{
    if(role==='staff'){
     setJobs(await apiRequest<Assignment[]>('staff/me/jobs'))
    }else if(route==='don'&&id){
     const detail=await apiRequest<Booking>(`${base}/${id}`)
     const [assignments,requests]=await Promise.all([apiRequest<Assignment[]>(role==='admin'?`staff/admin/bookings/${id}/assignments`:`orders/me/${id}/assignments`),apiRequest<Cancellation[]>(`${base}/${id}/cancellations`)])
     setBooking(detail);setJobs(assignments);setCancellations(requests)
     if(role==='admin'){
      const [staff,qualifications]=await Promise.all([apiPages<Employee>('staff/admin/employees'),apiRequest<Skill[]>('staff/admin/skills')]);setEmployees(staff);setSkills(qualifications)
     }
     else setReviews((await apiRequest<Page<Review>>(`reviews/me?bookingId=${id}&pageSize=100`)).items)
    }else if(route==='danh-gia')setReviews((await apiRequest<Page<Review>>(`reviews/admin/reviews?page=${page}&pageSize=20`)).items)
    else if(route==='tai-khoan'){
     const result=await apiRequest<Page<Account>>(`auth/accounts?page=${page}&pageSize=20&search=${encodeURIComponent(search)}`);setAccounts(result.items);setTotal(result.total)
    }else{
     const result=await apiRequest<Page<Summary>>(`${base}${route==='lich-su'?'/history':''}?page=${page}&pageSize=20${status?'&status='+status:''}${search?'&search='+encodeURIComponent(search):''}${role==='admin'&&trash?'&trash=true':''}`);setSummaries(result.items);setTotal(result.total)
    }
   }catch(e){setError(message(e))}
 },[base,id,page,role,route,search,status,trash])
 useEffect(()=>{let active=true;const timer=setTimeout(()=>{if(active)void load()},200);return()=>{active=false;clearTimeout(timer)}},[load,tick])
 useEffect(()=>{
  if(operation?.state!=='pending')return
  let active=true
  const timer=setTimeout(async()=>{try{const next=await apiRequest<Operation>(`orders/operations/${operation.id}`);if(!active)return;setOperation(next);if(next.state==='failed')setError(next.errorMessage||'Thao tác bị từ chối.');if(next.state==='succeeded'){setNotice('Máy chủ đã xử lý xong.');setTick(v=>v+1)}}catch(e){if(active){setError(message(e));setOperation({...operation})}}},2000)
  return()=>{active=false;clearTimeout(timer)}
 },[operation])
 async function mutate(endpoint:string,body:unknown,method='POST',workflow=false){
  if(locked.current||operation?.state==='pending')return
  locked.current=true;setBusy(true);setError('');setNotice('')
  const fingerprint=JSON.stringify([endpoint,method,body])
  const requestKey=retry.current?.fingerprint===fingerprint?retry.current.key:crypto.randomUUID();if(workflow)retry.current={fingerprint,key:requestKey}
  try{
   const result=await apiRequest<Operation>(endpoint,{method,body:JSON.stringify({...body as object,...(workflow?{idempotencyKey:requestKey}:{})})})
   if(workflow){setOperation(result);retry.current=null;if(result.state==='failed')throw new Error(result.errorMessage||'Thao tác bị từ chối.');if(result.state==='pending'){setNotice('Máy chủ đã nhận yêu cầu, đang xử lý.');return}}
   setNotice('Đã lưu trên máy chủ.');setReason('');setTick(v=>v+1)
  }catch(e){setError(message(e))}finally{locked.current=false;setBusy(false)}
 }
 useEffect(()=>{const refresh=()=>setTick(v=>v+1);window.addEventListener('cleanhome-jobs-changed',refresh);return()=>window.removeEventListener('cleanhome-jobs-changed',refresh)},[])
 const waiting=busy||operation?.state==='pending'
 const feedback=<>{error&&<div role="alert" className="my-4 rounded-xl border border-red-200 bg-red-50 p-4"><p>{error}</p><Btn secondary onClick={()=>{void load()}}>Tải lại dữ liệu</Btn></div>}{notice&&<p role="status" className="my-4 rounded-xl bg-blue-50 p-4">{notice}</p>}</>
 const pagination=<div className="ch-actions"><Btn secondary disabled={page===1} onClick={()=>setPage(v=>v-1)}>Trang trước</Btn><span>Trang {page}</span><Btn secondary disabled={page*20>=total} onClick={()=>setPage(v=>v+1)}>Trang sau</Btn></div>
 if(['thanh-toan','bao-cao','dat-dich-vu'].includes(route))return <div className="ch-container py-8"><Heading title={route==='dat-dich-vu'?'Đặt dịch vụ':route==='thanh-toan'?'Thanh toán':'Báo cáo'}/><Panel><p>Chức năng này đang chờ API backend hoàn thiện.</p></Panel></div>
 if(role==='staff')return <StaffWorkBoard jobs={jobs} id={id} feedback={feedback} waiting={waiting} progress={job=>void mutate(`orders/staff/orders/${job.bookingId}/progress`,{assignmentId:job.id,nextStatus:job.status==='waiting'?'accepted':job.status==='accepted'?'in_progress':'done'},'POST',true)}/>
 if(route==='tai-khoan')return <div className="ch-container py-8"><Heading title="Tài khoản"/>{feedback}<Field label="Tìm email hoặc ID"><input className={inputClass} value={search} onChange={e=>{setSearch(e.target.value);setPage(1)}}/></Field><Panel><Table headers={['ID','Email','Vai trò','Trạng thái','Thao tác']} rows={accounts.map(a=>[String(a.id),a.email,a.role,<Badge value={a.status}/>,<Btn disabled={waiting||a.role==='admin'} onClick={()=>void mutate(`auth/accounts/${a.id}/status`,{locked:a.status==='active'},'PUT')}>{a.status==='active'?'Khóa':'Mở khóa'}</Btn>])}/>{pagination}</Panel></div>
 if(route==='danh-gia')return <div className="ch-container py-8"><Heading title="Quản lý đánh giá"/>{feedback}<Field label="Lý do ẩn"><input className={inputClass} value={reason} onChange={e=>setReason(e.target.value)}/></Field><Panel><Table headers={['Đơn','Nhân viên','Sao','Nội dung','Hiển thị']} rows={reviews.map(r=>[String(r.bookingId),String(r.employeeId),String(r.rating),r.comment,<Btn disabled={waiting} onClick={()=>void mutate(`reviews/admin/reviews/${r.id}/visibility`,{isHidden:!r.isHidden,reason},'PATCH')}>{r.isHidden?'Hiện lại':'Ẩn'}</Btn>])}/><div className="ch-actions"><Btn disabled={page===1} onClick={()=>setPage(v=>v-1)}>Trước</Btn><Btn disabled={reviews.length<20} onClick={()=>setPage(v=>v+1)}>Sau</Btn></div></Panel></div>
 if(route==='don'&&id)return <div className="ch-container py-8"><Heading title={booking?`Đơn ${booking.code}`:'Chi tiết đơn'}/>{feedback}{booking&&<>{role==='admin'&&<><AdminOrderLifecycle bookingId={booking.id} status={booking.status} version={booking.version}/><AdminWithdrawals bookingId={booking.id}/></>}<OrderPayment bookingId={booking.id} role={role}/><BookingRecruitmentStatus bookingId={booking.id}/>
  <Panel><Badge value={booking.status}/><p>{date(booking.scheduledStart)} · {money(booking.totalAmount)}</p><p>{booking.specialRequest}</p><Table headers={['Dịch vụ','Số lượng','Đơn giá']} rows={booking.items.map(item=>[item.serviceName,String(item.quantity),money(item.unitPrice)])}/>{role==='admin'&&booking.status==='pending'&&<Btn disabled={waiting} onClick={()=>void mutate(`${base}/${id}/confirm`,{expectedVersion:booking.version})}>Xác nhận đơn</Btn>}</Panel>
  <Panel title="Nhân viên tham gia"><Table headers={['Mã nhân viên','Trạng thái','Thao tác']} rows={jobs.map(j=>[String(j.employeeId),<Badge value={j.status}/>,role==='admin'&&['waiting','accepted'].includes(j.status)?<Btn disabled={waiting} onClick={()=>void mutate(`${base}/${id}/unassign`,{assignmentId:j.id,expectedVersion:booking.version},'POST',true)}>Gỡ phân công</Btn>:null])}/></Panel>
  <Panel title="Yêu cầu hủy"><Field label="Lý do hoặc phản hồi"><textarea className={inputClass} value={reason} onChange={e=>setReason(e.target.value)} maxLength={2000}/></Field>{role==='customer'&&['pending_payment','pending','confirmed','in_progress'].includes(booking.status)&&!cancellations.some(c=>c.status==='pending')&&<Btn disabled={waiting} onClick={()=>void mutate(`${base}/${id}/cancellations`,{reason,expectedVersion:booking.version})}>Gửi yêu cầu hủy</Btn>}{cancellations.map(c=><div key={c.id} className="my-4"><p>{c.reason}</p><Badge value={c.status}/><p>{c.adminNote}</p>{role==='admin'&&c.status==='pending'&&<div className="ch-actions"><Btn disabled={waiting} onClick={()=>void mutate(`${base}/${id}/cancellations/${c.id}/approve`,{note:reason,expectedVersion:booking.version},'POST',true)}>Duyệt hủy</Btn><Btn secondary disabled={waiting} onClick={()=>void mutate(`${base}/${id}/cancellations/${c.id}/reject`,{note:reason,expectedVersion:booking.version})}>Từ chối</Btn></div>}</div>)}</Panel>
  {role==='admin'&&['confirmed','in_progress'].includes(booking.status)&&<Panel title="Phân công nhiều nhân viên"><p>Chọn nhân viên và kỹ năng bắt buộc. Máy chủ kiểm tra chi nhánh, xác minh, lịch và kỹ năng.</p><fieldset disabled={waiting}>{employees.map(e=><label key={e.id} className="m-2 inline-flex gap-2"><input type="checkbox" checked={selected.includes(e.id)} onChange={ev=>setSelected(v=>ev.target.checked?[...v,e.id]:v.filter(x=>x!==e.id))}/>Nhân viên {e.id} · chi nhánh {e.branchId}</label>)}<hr/>{skills.map(s=><label key={s.id} className="m-2 inline-flex gap-2"><input type="checkbox" checked={required.includes(s.id)} onChange={e=>setRequired(v=>e.target.checked?[...v,s.id]:v.filter(x=>x!==s.id))}/>{s.name}</label>)}</fieldset><Btn disabled={waiting||!selected.length} onClick={()=>void mutate(`${base}/${id}/assignments`,{employeeIds:selected,requiredSkillIds:required,expectedVersion:booking.version},'POST',true)}>Lưu phân công</Btn></Panel>}
  {role==='customer'&&booking.status==='completed'&&<Panel title="Đánh giá từng nhân viên"><Field label="Nhân viên"><select className={inputClass} value={reviewEmployee} onChange={e=>setReviewEmployee(e.target.value)}><option value="">Chọn nhân viên đã hoàn thành</option>{jobs.filter(j=>j.status==='done'&&!reviews.some(r=>r.employeeId===j.employeeId)).map(j=><option key={j.id} value={j.employeeId}>Nhân viên {j.employeeId}</option>)}</select></Field><Field label="Số sao"><select className={inputClass} value={rating} onChange={e=>setRating(Number(e.target.value))}>{[1,2,3,4,5].map(n=><option key={n}>{n}</option>)}</select></Field><Field label="Nhận xét"><textarea className={inputClass} value={reason} onChange={e=>setReason(e.target.value)}/></Field><Btn disabled={waiting||!reviewEmployee} onClick={()=>void mutate('reviews/me',{bookingId:Number(id),employeeId:Number(reviewEmployee),rating,comment:reason})}>Gửi đánh giá</Btn>{reviews.map(r=><p key={r.id}>Nhân viên {r.employeeId}: {r.rating}/5 — {r.comment}</p>)}</Panel>}
  <Panel title="Lịch sử trạng thái">{booking.history.map(h=><p key={h.id}>{date(h.createdAt)} · <Badge value={h.status}/> {h.note}</p>)}</Panel>
 </>}</div>
 return <div className="ch-container py-8"><Heading title={role==='admin'?'Quản lý đơn':route==='lich-su'?'Lịch sử dịch vụ':'Đơn của tôi'}/>{feedback}{role==='admin'&&<><AdminWithdrawals/><label className="my-4 flex gap-3"><input type="checkbox" checked={trash} onChange={e=>{setTrash(e.target.checked);setPage(1)}}/>Thùng rác / Đơn đã xóa</label></>}<Field label="Trạng thái"><select className={inputClass} value={status} onChange={e=>{setStatus(e.target.value);setPage(1)}}><option value="">Tất cả</option>{['pending_payment','pending','confirmed','in_progress','completed','cancelled'].map(s=><option key={s}>{s}</option>)}</select></Field><Panel><Table headers={['Mã đơn','Khách / dịch vụ / thanh toán','Lịch','Tổng tiền','Trạng thái','Chi tiết']} rows={summaries.map(s=>[s.code,role==='admin'?<AdminOrderOverview key={s.id+'-'+tick} bookingId={s.id}/>:<Link href={`/don/${s.id}`}>Chi tiết dịch vụ</Link>,date(s.scheduledStart),money(s.totalAmount),<Badge value={s.status}/>,<div className="flex flex-wrap gap-3"><Link href={`/don/${s.id}`}>Xem đơn</Link>{role==='admin'&&<Link className="ch-btn secondary" href={`/don/${s.id}`}>Hủy / Xóa / Khôi phục</Link>}</div>])}/>{pagination}</Panel></div>
}
