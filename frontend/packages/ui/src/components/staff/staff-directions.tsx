"use client"
import {useEffect,useState} from 'react'
import {NavigationIcon,MapPinIcon} from 'lucide-react'
import {apiRequest} from '@/lib/api-client'
import {directionsUrl,estimatedDistanceKm,type StaffDestination} from '@/lib/directions'
import {Button} from '@/components/ui/button'
import {LocationMap} from '../shared/location-map'

export function StaffDirections({bookingId}:{bookingId:number}){
 const [destination,setDestination]=useState<StaffDestination|null>(null),[error,setError]=useState(''),[retry,setRetry]=useState(0)
 const [origin,setOrigin]=useState<{latitude:number|null;longitude:number|null;fresh:boolean}|null>(null)
 useEffect(()=>{const controller=new AbortController();apiRequest<{latitude:number|null;longitude:number|null;fresh:boolean}>('staff/me/dispatch/presence',{signal:controller.signal}).then(value=>{if(!controller.signal.aborted)setOrigin(value)}).catch(()=>{});return()=>controller.abort()},[bookingId,retry])
 useEffect(()=>{
  const controller=new AbortController();setDestination(null);setError('')
  apiRequest<StaffDestination>(`orders/staff/orders/${bookingId}/destination`,{signal:controller.signal}).then(value=>{if(!controller.signal.aborted)setDestination(value)}).catch(error=>{if(!controller.signal.aborted)setError(error.message)})
  return()=>controller.abort()
 },[bookingId,retry])
 if(error)return <div className="min-w-48"><p role="alert" className="text-sm">{error}</p><Button variant="outline" onClick={()=>setRetry(v=>v+1)}>Thử tải địa chỉ lại</Button></div>
 if(!destination)return <p role="status" className="text-sm text-muted-foreground">Đang tải địa chỉ…</p>
 const href=directionsUrl(destination)
 const distance=origin?.fresh?estimatedDistanceKm(origin,destination):null
 return <div className="flex min-w-52 max-w-sm flex-col items-start gap-3 whitespace-normal"><p className="m-0 flex gap-2 text-sm"><MapPinIcon aria-hidden="true" className="mt-0.5 size-4 shrink-0"/>{destination.addressText||'Chưa có địa chỉ dạng chữ'}</p>{destination.recipient&&<p className="m-0 text-sm">{destination.recipient}{destination.phone?` · ${destination.phone}`:''}</p>}{destination.latitude!==null&&destination.longitude!==null&&<div className="w-full"><LocationMap readOnly value={{latitude:destination.latitude,longitude:destination.longitude}}/></div>}<p className="text-sm text-muted-foreground">{distance!==null?`Khoảng cách ước tính: ${distance.toLocaleString('vi-VN')} km đường chim bay.`:'Chưa có vị trí nhân viên đủ mới để ước tính khoảng cách. Google Maps sẽ xác định đường đi khi bạn mở chỉ đường.'}</p>{href?<Button render={<a href={href} target="_blank" rel="noopener noreferrer"/>} nativeButton={false}><NavigationIcon aria-hidden="true"/>Chỉ đường đến khách hàng</Button>:<p className="text-sm text-muted-foreground">Chưa có địa chỉ hợp lệ để dẫn đường.</p>}</div>
}
