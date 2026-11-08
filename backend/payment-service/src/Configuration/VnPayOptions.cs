using CleanHome.ServiceDefaults;
namespace CleanHome.Payment.Api.Configuration;
public sealed class VnPayOptions(IConfiguration config){
 public string Get(string key)=>config["VNPAY_"+key]??key switch{"TMN_CODE"=>config["VnPay:TmnCode"]??"","HASH_SECRET"=>config["VnPay:HashSecret"]??"","PAYMENT_URL"=>config["VnPay:PaymentUrl"]??"https://sandbox.vnpayment.vn/paymentv2/vpcpay.html","RETURN_URL"=>config["VnPay:ReturnUrl"]??"","IPN_URL"=>config["VnPay:IpnUrl"]??"",_=>""};
 public string Frontend=>config["PAYMENT_FRONTEND_URL"]??"http://localhost:3001";
 public void Require(){if(string.IsNullOrWhiteSpace(Get("TMN_CODE"))||string.IsNullOrWhiteSpace(Get("HASH_SECRET"))||!Uri.TryCreate(Get("RETURN_URL"),UriKind.Absolute,out var uri)||uri.Scheme!="https"||Get("PAYMENT_URL")!="https://sandbox.vnpayment.vn/paymentv2/vpcpay.html")throw new ApiException(503,"Chưa cấu hình VNPay Sandbox. Kiểm tra .env của payment-service.");}
 // Dedicated service .env, lower priority than real environment variables. Never logged.
 public static void LoadEnv(string root){var path=Path.GetFullPath(Path.Combine(root,"..",".env"));if(!File.Exists(path))return;foreach(var line in File.ReadLines(path)){var text=line.Trim();if(text.Length==0||text.StartsWith('#'))continue;var split=text.IndexOf('=');if(split<1)continue;var key=text[..split].Trim();if(!key.StartsWith("VNPAY_",StringComparison.Ordinal)&&key!="PAYMENT_FRONTEND_URL")continue;if(Environment.GetEnvironmentVariable(key)is null)Environment.SetEnvironmentVariable(key,text[(split+1)..].Trim().Trim('"','\''));}}
}
