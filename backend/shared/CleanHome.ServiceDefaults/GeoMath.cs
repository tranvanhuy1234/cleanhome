namespace CleanHome.ServiceDefaults;
public static class GeoMath
{
 public static double DistanceKm(double lat1,double lon1,double lat2,double lon2)
 {
  var latitude=(lat2-lat1)*Math.PI/180;var longitude=(lon2-lon1)*Math.PI/180;
  var h=Math.Pow(Math.Sin(latitude/2),2)+Math.Cos(lat1*Math.PI/180)*Math.Cos(lat2*Math.PI/180)*Math.Pow(Math.Sin(longitude/2),2);
  return 6371*2*Math.Asin(Math.Sqrt(Math.Clamp(h,0,1)));
 }
}
