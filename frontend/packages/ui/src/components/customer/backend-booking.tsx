"use client"
import {PaymentMethod} from './payment-method'
import {BookingLocationEditor,type ConfirmedLocation} from './booking-location-editor'
import {useEffect,useRef,useState} from 'react'
import {useRouter} from 'next/navigation'
import Link from 'next/link'
import {apiRequest} from '@/lib/api-client'
import {useAppState} from '@/lib/store'
import {Heading,Panel,Field,Btn,money} from '../../common'
type Address={id:number;recipient:string;phone:string;addressText:string;latitude:number;longitude:number}
type Branch={id:number;name:string;addressText:string}
type Created={id:number;code:string;totalAmount:number}
export function BackendBooking({id}:{id?:string}){
 const state=useAppState(),router=useRouter()
 const [serviceId,setService]=useState(id||''),[quantity,setQuantity]=useState(1),[addressId,setAddress]=useState(''),[branchId,setBranch]=useState(''),[scheduled,setScheduled]=useState(''),[note,setNote]=useState(''),[addresses,setAddresses]=useState<Address[]>([]),[branches,setBranches]=useState<Branch[]>([]),[error,setError]=useState(''),[busy,setBusy]=useState(false),[loaded,setLoaded]=useState(false),[tick,setTick]=useState(0)
 const [paymentMethod,setPaymentMethod]=useState<'CASH'|'VNPAY'>('CASH')
 const [location,setLocation]=useState<ConfirmedLocation|null>(null),[requiredStaff,setRequiredStaff]=useState(1)
 useEffect(()=>{const saved=addresses.find(a=>String(a.id)===addressId);setLocation(saved?{addressText:saved.addressText,latitude:saved.latitude,longitude:saved.longitude,confirmed:false}:null)},[addressId,addresses])
 const locked=useRef(false),retry=useRef<{body:string;key:string}|null>(null)
 useEffect(()=>{if(error)document.getElementById('booking-form-error')?.scrollIntoView({block:'center',behavior:'smooth'})},[error])
 useEffect(()=>{let live=true;setError('');setLoaded(false);Promise.all([apiRequest<Address[]>('users/me/addresses'),apiRequest<Branch[]>('orders/booking/branches')]).then(([a,b])=>{if(live){setAddresses(a);setBranches(b);setAddress(v=>v||String(a[0]?.id||''));setBranch(v=>v||String(b[0]?.id||''));setLoaded(true)}}).catch(e=>{if(live)setError(e.message)});return()=>{live=false}},[tick])
 useEffect(()=>{if(loaded&&window.location.hash==='#thong-tin-nha-o'){const section=document.getElementById('thong-tin-nha-o');section?.scrollIntoView({block:'start'});section?.focus({preventScroll:true})}},[loaded])
 const service=state?.services.find(s=>s.id===(serviceId||state.services[0]?.id))
 const amount=service?service.price*quantity:0,minutes=service?service.minutesPerUnit*quantity:0
 async function submit(event:React.FormEvent){
  event.preventDefault();if(locked.current||!service)return
  setError('');locked.current=true;setBusy(true)
  try{
   if(!scheduled||!Number.isFinite(new Date(scheduled).getTime())||new Date(scheduled).getTime()<Date.now()+30*60000)throw new Error('Lịch thực hiện phải sau thời điểm hiện tại ít nhất 30 phút. Hãy chọn lại ngày giờ, rồi thanh toán.');
   const data={paymentMethod,automaticDispatch:true,requiredStaff,location,serviceId:Number(service.id),addressId:Number(addressId),branchId:Number(branchId),quantity,scheduledStart:new Date(scheduled).toISOString(),specialRequest:note.trim()||null}
   const body=JSON.stringify(data)
   const key=retry.current?.body===body?retry.current.key:crypto.randomUUID();retry.current={body,key}
   const created=await apiRequest<Created>('orders/booking',{method:'POST',body:JSON.stringify({...data,idempotencyKey:key})})
   retry.current=null;
   if(paymentMethod==='VNPAY'){try{const result=await apiRequest<{paymentUrl:string}>('payments/vnpay/create',{method:'POST',body:JSON.stringify({orderId:created.id,idempotencyKey:'checkout-'+key})});const url=new URL(result.paymentUrl);if(url.protocol!=='https:'||url.hostname!=='sandbox.vnpayment.vn')throw new Error('Invalid payment URL');window.location.assign(result.paymentUrl);return}catch{router.push(`/don/${created.id}?payment=retry`);return}}
   router.push(`/don/${created.id}`)
  }catch(e){setError(e instanceof Error?e.message:'Không đặt được dịch vụ. Thông tin đã nhập được giữ để thử lại.');locked.current=false;setBusy(false)}
 }
 const input='min-h-12 w-full rounded-xl border border-input bg-white px-3 py-2'
 return <section className="mx-auto max-w-4xl px-4 py-8"><Heading title="Đặt dịch vụ" sub="Chọn dịch vụ, địa chỉ và lịch mong muốn. Yêu cầu của bạn sẽ chờ CleanHome xác nhận."/>{error&&<div id="booking-form-error" role="alert" className="my-4 rounded-xl border border-red-200 bg-red-50 p-4"><p>{error}</p>{!loaded&&<Btn secondary onClick={()=>setTick(v=>v+1)}>Tải lại</Btn>}</div>}{!loaded&&!error&&<p role="status">Đang tải thông tin đặt dịch vụ…</p>}<form onSubmit={submit} className="grid gap-5"><fieldset disabled={busy||!loaded} className="grid gap-5"><Panel title="Dịch vụ và lịch hẹn"><div className="grid gap-4"><Field label="Dịch vụ"><select required className={input} value={service?.id||''} onChange={e=>setService(e.target.value)}><option value="">Chọn dịch vụ</option>{state?.services.map(s=><option key={s.id} value={s.id}>{s.name} — {money(s.price)}/{s.unit}</option>)}</select></Field><Field label={`Số lượng (${service?.unit||'đơn vị'})`}><input className={input} type="number" required min={1} max={100} step={1} value={quantity} onChange={e=>setQuantity(Number(e.target.value))}/></Field><Field label="Chi nhánh tiếp nhận"><select required className={input} value={branchId} onChange={e=>setBranch(e.target.value)}><option value="">Chọn chi nhánh</option>{branches.map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select></Field><Field label="Ngày giờ thực hiện" hint="Chọn trước ít nhất 30 phút, trong 90 ngày tới; toàn bộ công việc nằm trong 08:00–18:00 giờ Việt Nam."><input required className={input} type="datetime-local" value={scheduled} onChange={e=>setScheduled(e.target.value)}/></Field></div></Panel>
 <section id="thong-tin-nha-o" tabIndex={-1} aria-label="Thông tin nhà ở" className="scroll-mt-28 outline-none"> <Panel title="Thông tin nhà ở"><div className="grid gap-4"><Field label="Địa chỉ đã lưu"><select required className={input} value={addressId} onChange={e=>setAddress(e.target.value)}><option value="">Chọn địa chỉ</option>{addresses.map(a=><option key={a.id} value={a.id}>{a.recipient} · {a.phone} · {a.addressText}</option>)}</select></Field><BookingLocationEditor value={location} onChange={setLocation} requiredStaff={requiredStaff} onStaffChange={setRequiredStaff}/>{loaded&&!addresses.length&&<p>Bạn chưa có địa chỉ. <Link className="underline" href="/ho-so">Thêm địa chỉ tại hồ sơ</Link>, sau đó quay lại đặt dịch vụ.</p>}<Field label="Yêu cầu thêm"><textarea className={input} rows={4} maxLength={2000} value={note} onChange={e=>setNote(e.target.value)}/></Field></div></Panel></section><Panel title="Kiểm tra yêu cầu"><PaymentMethod value={paymentMethod} onChange={setPaymentMethod}/><p>{service?.name} · {scheduled||'Chưa chọn lịch'} · {location?.addressText}</p><p>Thời lượng dự kiến: {minutes} phút</p><p className="text-xl font-semibold">Tổng giá tham khảo: {money(amount)}</p><p>Giá cuối cùng được tính theo bảng giá hiện tại khi gửi yêu cầu. Đặt dịch vụ chưa xác nhận lịch nhân viên và chưa thu tiền.</p><Btn type="submit" disabled={!service||!addressId||!branchId||!location?.confirmed||busy}>{busy?'Đang gửi yêu cầu…':paymentMethod==='VNPAY'?'Thanh toán qua VNPay':'Xác nhận đặt dịch vụ'}</Btn></Panel></fieldset></form></section>
}
