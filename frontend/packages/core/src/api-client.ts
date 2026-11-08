import {API_URL} from './env'
let token: string | null | undefined
export function accessToken() { if(token !== undefined)return token; try{return token=window.sessionStorage.getItem('cleanhome-api-session')}catch{return null} }
export function setAccessToken(value: string | null) { token=value; try {if(value)window.sessionStorage.setItem('cleanhome-api-session',value);else window.sessionStorage.removeItem('cleanhome-api-session')}catch{} }
export class ApiError extends Error { constructor(public status:number,message:string){super(message)} }
/** HTTP qua gateway; token phiên riêng từng tab, không lưu mật khẩu. */
export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if(init.body)headers.set('Content-Type','application/json')
  if(accessToken()&&!headers.has('Authorization'))headers.set('Authorization',`Bearer ${accessToken()}`)
  let response: Response
  try { response = await fetch(`${API_URL.replace(/\/$/, '')}/${path.replace(/^\//, '')}`, {
    ...init,
    cache:'no-store',signal:init.signal??AbortSignal.timeout(15000),headers,
  }) } catch { throw new ApiError(0,'Không kết nối được máy chủ. Vui lòng kiểm tra backend và thử lại.') }
  if (!response.ok) {
    const problem=await response.json().catch(()=>null)
    if(response.status===401){setAccessToken(null);window.dispatchEvent(new Event('cleanhome-session-expired'))}
    throw new ApiError(response.status,Object.values(problem?.errors??{}).flat().join(' ')||problem?.detail||problem?.title||`Yêu cầu API thất bại (${response.status}).`)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

/** Load reference lists without silently dropping records after the first page. */
export async function apiPages<T>(path: string): Promise<T[]> {
  const items:T[]=[]
  for(let page=1;;page++){
    const result=await apiRequest<{items:T[];total:number}>(`${path}${path.includes('?')?'&':'?'}page=${page}&pageSize=100`)
    items.push(...result.items)
    if(items.length>=result.total||!result.items.length)return items
  }
}
