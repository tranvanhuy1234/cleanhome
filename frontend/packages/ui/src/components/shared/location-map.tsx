"use client"
import {useEffect,useRef,useState} from 'react'
import type {Map as LeafletMap,Marker} from 'leaflet'
import 'leaflet/dist/leaflet.css'
import {Button} from '@/components/ui/button'
export type GeoPoint={latitude:number;longitude:number}
export function validPoint(value:GeoPoint|null):value is GeoPoint{return !!value&&Number.isFinite(value.latitude)&&Number.isFinite(value.longitude)&&Math.abs(value.latitude)<=90&&Math.abs(value.longitude)<=180}
export function LocationMap({value,onChange,readOnly=false}:{value:GeoPoint|null;onChange?:(value:GeoPoint)=>void;readOnly?:boolean}){
 const container=useRef<HTMLDivElement>(null),map=useRef<LeafletMap|null>(null),marker=useRef<Marker|null>(null),latest=useRef(value),change=useRef(onChange)
 latest.current=value;change.current=onChange
 const [error,setError]=useState(''),[busy,setBusy]=useState(false)
 useEffect(()=>{
  let live=true
  import('leaflet').then(L=>{
   if(!live||!container.current)return
   const center=validPoint(latest.current)?[latest.current.latitude,latest.current.longitude] as [number,number]:[10.7769,106.7009] as [number,number]
   const instance=L.map(container.current,{scrollWheelZoom:false}).setView(center,validPoint(latest.current)?16:12);map.current=instance
   L.tileLayer(process.env.NEXT_PUBLIC_MAP_TILE_URL||'https://tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19,attribution:'&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors'}).on('tileerror',()=>{if(live)setError('Chưa tải được ảnh bản đồ. Điểm ghim vẫn được giữ; bạn có thể thử lại khi có mạng.');}).addTo(instance)
   const pin=L.marker(center,{draggable:!readOnly,icon:L.divIcon({className:'',html:'<span style="font-size:32px;line-height:36px">📍</span>',iconSize:[32,36],iconAnchor:[16,34]}),title:'Địa điểm vệ sinh'});marker.current=pin
   if(validPoint(latest.current))pin.addTo(instance)
   if(!readOnly){instance.on('click',event=>{pin.setLatLng(event.latlng).addTo(instance);change.current?.({latitude:event.latlng.lat,longitude:event.latlng.lng})});pin.on('dragend',()=>{const p=pin.getLatLng();change.current?.({latitude:p.lat,longitude:p.lng})})}
  }).catch(()=>{if(live)setError('Không mở được bản đồ. Bạn vẫn có thể nhập tọa độ hoặc thử lại sau.')})
  return()=>{live=false;map.current?.remove();map.current=null;marker.current=null}
 },[readOnly])
 useEffect(()=>{if(validPoint(value)&&map.current&&marker.current){marker.current.setLatLng([value.latitude,value.longitude]).addTo(map.current);map.current.panTo([value.latitude,value.longitude])}},[value])
 function locate(){if(!navigator.geolocation){setError('Trình duyệt không hỗ trợ định vị. Hãy chọn điểm trên bản đồ.');return}setBusy(true);setError('');navigator.geolocation.getCurrentPosition(position=>{setBusy(false);change.current?.({latitude:position.coords.latitude,longitude:position.coords.longitude})},()=>{setBusy(false);setError('Không lấy được vị trí. Bạn có thể chọn điểm trên bản đồ; vị trí hiện tại không mặc định là địa chỉ vệ sinh.')},{enableHighAccuracy:true,timeout:15000,maximumAge:30000})}
 return <div className="space-y-3">{!readOnly&&<><Button type="button" variant="outline" disabled={busy} onClick={locate}>{busy?'Đang lấy vị trí…':'Sử dụng vị trí hiện tại'}</Button><p className="text-sm leading-6 text-muted-foreground">Bấm trên bản đồ hoặc kéo ghim đến đúng địa điểm vệ sinh. Chỉ dùng vị trí hiện tại nếu đó là nơi bạn muốn đặt dịch vụ.</p></>}<div ref={container} className="relative isolate z-0 h-72 w-full overflow-hidden rounded-xl border" aria-label={readOnly?'Bản đồ địa điểm vệ sinh':'Bản đồ chọn địa điểm vệ sinh'}/>{error&&<p role="status" className="text-sm text-amber-800">{error}</p>}{value&&<p className="text-xs text-muted-foreground">Ghim: {value.latitude.toFixed(6)}, {value.longitude.toFixed(6)}</p>}{!readOnly&&<div className="grid grid-cols-2 gap-3"><label className="text-sm">Vĩ độ<input aria-label="Vĩ độ điểm vệ sinh" type="number" step="any" min={-90} max={90} value={value?.latitude??''} className="mt-1 min-h-11 w-full rounded-lg border px-3" onChange={e=>change.current?.({latitude:Number(e.target.value),longitude:value?.longitude??106.7009})}/></label><label className="text-sm">Kinh độ<input aria-label="Kinh độ điểm vệ sinh" type="number" step="any" min={-180} max={180} value={value?.longitude??''} className="mt-1 min-h-11 w-full rounded-lg border px-3" onChange={e=>change.current?.({latitude:value?.latitude??10.7769,longitude:Number(e.target.value)})}/></label></div>}</div>
}
