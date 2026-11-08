"use client"
import {apiRequest,accessToken,setAccessToken,ApiError} from './api-client'
import {APP_ROLE} from './env'
import type {AppState,User,Service,Role} from './types'
type Account={id:number;email:string;role:Role}
type Profile={fullName:string;phone:string;avatarUrl?:string;createdAt:string}
type Item={id:number;categoryId:number;categoryName:string;name:string;description:string;unit:string;basePrice:number;durationMinutes:number}
const empty=():AppState=>({version:1,users:[],services:[],orders:[],reviews:[],payments:[],passwordHashes:{},session:null})
let state=empty(),started=false,revision=0
let error=''
let pending:Promise<void>|null=null
const listeners=new Set<()=>void>()
function emit(){revision++;listeners.forEach(fn=>fn())}
export const backendSnapshot=()=>state
let status={status:'ready' as const,revision:0,dirty:false,error:''}
export function backendStatus(){if(status.revision!==revision)status={...status,revision,error};return status}
function user(a:Account,p?:Profile):User{return{id:String(a.id),code:String(a.id),email:a.email,role:a.role,name:p?.fullName||a.email,phone:p?.phone||'',address:'',avatar:p?.avatarUrl,status:'active',createdAt:p?.createdAt||''}}
function service(s:Item):Service{return{id:String(s.id),slug:String(s.id),name:s.name,description:s.description,shortDesc:s.description,unit:s.unit,price:s.basePrice,minutesPerUnit:s.durationMinutes,baseMinutes:0,category:String(s.categoryId) as Service['category'],categoryName:s.categoryName,scope:[],included:[],excluded:[],quantityLabel:s.unit,minQuantity:1,defaultQuantity:1,image:'/images/hero.png',status:'active'}}
export function subscribeBackend(fn:()=>void){listeners.add(fn);if(!started){started=true;window.addEventListener('cleanhome-session-expired',()=>{state={...empty(),services:state.services};emit()});window.addEventListener('focus',()=>{void refreshBackend()});void refreshBackend()}return()=>{listeners.delete(fn)}}
export async function refreshBackend(){
 if(pending)return pending
 const originalToken=accessToken()
 pending=(async()=>{error='';try{
  const items:Item[]=[]
  for(let page=1;;page++){const data=await apiRequest<{items:Item[];total:number}>(`catalog/services?page=${page}&pageSize=100`);items.push(...data.items);if(items.length>=data.total||!data.items.length)break}
  state={...state,services:items.map(service)};emit()
  if(originalToken){const a=await apiRequest<Account>('auth/session');if(originalToken!==accessToken())return
   if(a.role!==APP_ROLE){setAccessToken(null);state={...empty(),services:state.services};throw new Error('Vui lòng đăng nhập đúng website của vai trò tài khoản.')}
   let p:Profile|undefined;try{p=await apiRequest<Profile>('users/me')}catch(e){if(!(e instanceof ApiError&&e.status===404))throw e}
   if(originalToken===accessToken())state={...state,users:[user(a,p)],session:{userId:String(a.id),role:a.role}}
  }
 }catch(e){error=e instanceof Error?e.message:'Không kết nối được máy chủ.'}finally{pending=null;emit()}})()
 return pending
}
export async function backendLogin(email:string,password:string,registration?:{name:string;phone:string}){
 try{const a=await apiRequest<Account&{accessToken:string}>(registration?'auth/register':'auth/login',{method:'POST',body:JSON.stringify(registration?{email:email.trim(),password,fullName:registration.name.trim(),phone:registration.phone.trim()}:{email:email.trim(),password})})
 if(a.role!==APP_ROLE){await apiRequest('auth/logout',{method:'POST',headers:{Authorization:`Bearer ${a.accessToken}`}}).catch(()=>undefined);return{ok:false as const,error:'Vui lòng đăng nhập tại website đúng vai trò.'}}
 setAccessToken(a.accessToken);state={...empty(),services:state.services,users:[user(a)],session:{userId:String(a.id),role:a.role}};emit();await pending;await refreshBackend()
 return{ok:true as const,user:state.users[0]||user(a),persistence:'server' as const}
 }catch(e){return{ok:false as const,error:e instanceof Error?e.message:'Không kết nối được máy chủ.'}}
}
export async function backendLogout(){try{if(accessToken())await apiRequest('auth/logout',{method:'POST'});setAccessToken(null);state={...empty(),services:state.services};emit();return{ok:true as const,data:undefined}}catch(e){return{ok:false as const,error:e instanceof Error?e.message:'Không đăng xuất được.'}}}
export async function backendSaveProfile(id:string,patch:Pick<User,'name'|'email'|'phone'|'address'>&{avatar?:string}){
 const current=state.users.find(u=>u.id===id)
 if(!current||state.session?.userId!==id)return{ok:false as const,error:'Không có quyền sửa hồ sơ.'}
 if(patch.email.trim()!==current.email||patch.address.trim()!==current.address)return{ok:false as const,error:'Đổi email cần xác nhận mật khẩu; địa chỉ cần tọa độ. Hiện chỉ lưu họ tên, điện thoại và URL ảnh.'}
 if(patch.avatar?.startsWith('data:'))return{ok:false as const,error:'Máy chủ chưa hỗ trợ tải ảnh, vui lòng dùng URL ảnh.'}
 try{await apiRequest('users/me',{method:'PUT',body:JSON.stringify({fullName:patch.name.trim(),phone:patch.phone.trim(),avatarUrl:patch.avatar||null})});await refreshBackend();return{ok:true as const,persistence:'server' as const}}catch(e){return{ok:false as const,error:e instanceof Error?e.message:'Không lưu được hồ sơ.'}}
}
