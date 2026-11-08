export type StaffDestination={bookingId:number;code:string;recipient:string;phone:string;addressText:string;latitude:number|null;longitude:number|null}

export function estimatedDistanceKm(origin:{latitude:number|null;longitude:number|null},destination:{latitude:number|null;longitude:number|null}):number|null{
 const values=[origin.latitude,origin.longitude,destination.latitude,destination.longitude]
 if(values.some(v=>typeof v!=='number'||!Number.isFinite(v))||Math.abs(origin.latitude!)>90||Math.abs(destination.latitude!)>90||Math.abs(origin.longitude!)>180||Math.abs(destination.longitude!)>180)return null
 const rad=(degrees:number)=>degrees*Math.PI/180
 const a=Math.sin(rad(destination.latitude!-origin.latitude!)/2)**2+Math.cos(rad(origin.latitude!))*Math.cos(rad(destination.latitude!))*Math.sin(rad(destination.longitude!-origin.longitude!)/2)**2
 return Math.round(6371*2*Math.asin(Math.sqrt(Math.max(0,Math.min(1,a))))*10)/10
}

/** Google Maps chooses the device's current location; no location is sent to our API. */
export function directionsUrl(destination:Pick<StaffDestination,'latitude'|'longitude'|'addressText'>):string|null{
 const {latitude,longitude}=destination
 const pinned=typeof latitude==='number'&&typeof longitude==='number'&&Number.isFinite(latitude)&&Number.isFinite(longitude)&&Math.abs(latitude)<=90&&Math.abs(longitude)<=180
 const target=pinned?`${latitude},${longitude}`:destination.addressText.trim()
 if(!target)return null
 const query=new URLSearchParams({api:'1',destination:target,dir_action:'navigate'})
 return `https://www.google.com/maps/dir/?${query}`
}
