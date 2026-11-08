using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using CleanHome.Payment.Api.Configuration;
using CleanHome.Payment.Api.Controllers;
using CleanHome.Payment.Api.Interfaces;
using CleanHome.Payment.Api.Models;
using CleanHome.Payment.Api.Repositories;
using CleanHome.Payment.Api.Services;
using CleanHome.Order.Api.Repositories;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using PaymentAssignment=CleanHome.Payment.Api.Models.Assignment;
internal static class PaymentChecks{
 static void Check(bool ok,string message){if(!ok)throw new Exception("Payment QA: "+message);}
 static int Port(){var l=new TcpListener(IPAddress.Loopback,0);l.Start();var p=((IPEndPoint)l.LocalEndpoint).Port;l.Stop();return p;}
 public static async Task RunAsync(string root,string adminConnection){
  var tag=Guid.NewGuid().ToString("N");var orderDb="cleanhome_test_pay_"+tag+"_order";var paymentDb="cleanhome_test_pay_"+tag+"_payment";
  await using var admin=new MySqlConnection(adminConnection);await admin.OpenAsync();WebApplication? source=null,app=null;
  try{
   foreach(var pair in new[]{("order",orderDb,new[]{"001_initial.sql","003_booking_creation.sql","004_dispatch.sql","005_payments.sql","006_order_lifecycle.sql"}),("payment",paymentDb,new[]{"001_initial.sql","002_checkout.sql"})})foreach(var file in pair.Item3){var sql=(await File.ReadAllTextAsync(Path.Combine(root,"databases",pair.Item1+"-db",file))).Replace("cleanhome_"+pair.Item1,pair.Item2,StringComparison.Ordinal);await using var q=new MySqlCommand(sql,admin);await q.ExecuteNonQueryAsync();}
   IConfiguration Config(string db)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:Database",new MySqlConnectionStringBuilder(adminConnection){Database=db}.ConnectionString}}).Build();
   var orderFactory=new DatabaseConnectionFactory(Config(orderDb));var orderRepo=new OrderPaymentRepository(orderFactory);var bookingRepo=new BookingCreationRepository(orderFactory);var assignments=new Dictionary<long,PaymentAssignment[]>();
   var secret="QA_ONLY_NOT_A_MERCHANT_SECRET";var key=Guid.NewGuid().ToString("N");var sourcePort=Port();var appPort=Port();while(appPort==sourcePort)appPort=Port();
   var sourceBuilder=WebApplication.CreateBuilder();sourceBuilder.WebHost.UseUrls("http://127.0.0.1:"+sourcePort);sourceBuilder.Logging.ClearProviders();source=sourceBuilder.Build();
   var actors=new Dictionary<string,(long,string)>{{new string('A',64),(10,"customer")},{new string('B',64),(21,"staff")},{new string('C',64),(22,"staff")},{new string('D',64),(1,"admin")},{new string('E',64),(11,"customer")}};
   source.MapGet("/session",(HttpRequest req)=>actors.TryGetValue(SessionTokens.Bearer(req)??"",out var actor)?Results.Ok(new SessionIdentity(actor.Item1,"qa@example.local",actor.Item2,DateTime.UtcNow.AddHours(1))):Results.Unauthorized());
   var mirrorUnavailable=false;
   bool Internal(HttpRequest req)=>req.Headers["X-CleanHome-Service-Key"]==key;
   source.MapGet("/internal/payments/{id:long}",async(long id,HttpRequest req)=>Internal(req)?Results.Ok(await orderRepo.FactAsync(id,default)):Results.Unauthorized());
   source.MapGet("/internal/payments/{id:long}/assignments",(long id,HttpRequest req)=>Internal(req)?Results.Ok(assignments.GetValueOrDefault(id)??[]):Results.Unauthorized());
   source.MapPost("/internal/payments/{id:long}",async(long id,PaymentMirror message,HttpRequest req)=>{if(!Internal(req))return Results.Unauthorized();if(mirrorUnavailable)return Results.StatusCode(503);await orderRepo.MirrorAsync(id,message,default);return Results.NoContent();});await source.StartAsync();
   var builder=WebApplication.CreateBuilder();builder.WebHost.UseUrls("http://127.0.0.1:"+appPort);builder.AddCleanHomeService(authentication:true);builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:Database",Config(paymentDb).GetConnectionString("Database")},{"InternalApi:Key",key},{"Services:OrderUrl","http://127.0.0.1:"+sourcePort},{"Services:AuthUrl","http://127.0.0.1:"+sourcePort},{"VNPAY_TMN_CODE","QA123456"},{"VNPAY_HASH_SECRET",secret},{"VNPAY_PAYMENT_URL","https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"},{"VNPAY_RETURN_URL","https://qa.example.local/api/payments/vnpay/return"}});builder.Logging.ClearProviders();
   builder.Services.AddControllers().AddApplicationPart(typeof(PaymentsController).Assembly);builder.Services.AddSingleton<VnPayOptions>();builder.Services.AddScoped<IPaymentRepository,PaymentRepository>();builder.Services.AddScoped<OrderPaymentClient>();builder.Services.AddScoped<VnPayGateway>();builder.Services.AddScoped<PaymentService>();builder.Services.AddHttpClient("payment-internal");builder.Services.AddHttpClient("vnpay");app=builder.Build();app.MapCleanHomeService("payment-qa",authentication:true);await app.StartAsync();
   using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri("http://127.0.0.1:"+appPort)};
   async Task<JsonElement> Request(string method,string path,int expected,object? body=null,char? actor='A'){using var req=new HttpRequestMessage(new HttpMethod(method),path);if(actor is not null)req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",new string(actor.Value,64));if(body is not null)req.Content=JsonContent.Create(body);using var res=await http.SendAsync(req);Check((int)res.StatusCode==expected,method+" "+path.Split('?')[0]+" expected "+expected+" got "+(int)res.StatusCode);if(res.Content.Headers.ContentLength==0||expected==204||expected==302)return default;var text=await res.Content.ReadAsStringAsync();return string.IsNullOrEmpty(text)?default:JsonDocument.Parse(text).RootElement.Clone();}
   async Task<long> New(string method){var b=await bookingRepo.CreateAsync(new(10,Guid.NewGuid().ToString("N"),"qa",1,new BookingService(1,"QA cleaning","hour",100000,60),new BookingAddress(1,"QA customer","0900000000","QA destination",10.77m,106.7m),1,DateTime.UtcNow.Date.AddDays(2).AddHours(2),DateTime.UtcNow.Date.AddDays(2).AddHours(3),null,2,true,"QA branch",method),default);assignments[b.Id]=[];return b.Id;}
   async Task Sync(long id){using var scope=app.Services.CreateScope();await scope.ServiceProvider.GetRequiredService<PaymentService>().Sync(id,default);}
   async Task<long> Scalar(string db,string sql,params(string,object?)[] args){await using var c=await new DatabaseConnectionFactory(Config(db)).OpenAsync();await using var q=c.Sql(sql,args);return Convert.ToInt64(await q.ExecuteScalarAsync());}
   async Task Work(long id,string status){await using var c=await orderFactory.OpenAsync();await using var q=c.Sql("UPDATE bookings SET status=@status WHERE id=@id",("id",id),("status",status));await q.ExecuteNonQueryAsync();}
   string Callback(string reference,string response="00",string state="00",long amount=10000000,string? transaction=null){var fields=new Dictionary<string,string>{{"vnp_TmnCode","QA123456"},{"vnp_TxnRef",reference},{"vnp_Amount",amount.ToString()},{"vnp_ResponseCode",response},{"vnp_TransactionStatus",state},{"vnp_TransactionNo",transaction??Random.Shared.NextInt64(100000000,999999999).ToString()},{"vnp_PayDate",DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss")}};var canonical=VnPayGateway.Canonical(fields);return canonical+"&vnp_SecureHash="+VnPayGateway.Sign(secret,canonical);}
   async Task<(string Ref,string Query)> Checkout(long id){var tx=await Request("POST","/vnpay/create",200,new{orderId=id,idempotencyKey=Guid.NewGuid().ToString("N")});var reference=tx.GetProperty("reference").GetString()!;return (reference,Callback(reference));}
   async Task<string> Ipn(string query)=>(await Request("GET","/vnpay/ipn?"+query,200,actor:null)).GetProperty("RspCode").GetString()!;
   var online=await New("VNPAY");Check((await orderRepo.FactAsync(online,default)).WorkStatus=="pending_payment","VNPay held before payment");Check(await Scalar(orderDb,"SELECT COUNT(*) FROM booking_dispatch WHERE booking_id=@id AND state='queued'",("id",online))==0,"no GPS queue before verified payment");
   var tx=await Checkout(online);var urlReplay=await Request("POST","/vnpay/create",200,new{orderId=online,idempotencyKey=Guid.NewGuid().ToString("N")});Check(urlReplay.GetProperty("reference").GetString()==tx.Ref,"concurrent clicks reuse active transaction");
   await Request("GET","/vnpay/return?"+tx.Query,302,actor:null);Check((await Request("GET",$"/orders/{online}/status",200)).GetProperty("status").GetString()=="PENDING","Return never marks paid / closed browser remains pending");
   Check(await Ipn(tx.Query+"bad")=="97","invalid signature");Check(await Ipn(Callback(tx.Ref,amount:1))=="04","wrong amount");
   var paidResults=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>Ipn(tx.Query)));Check(paidResults.Count(x=>x=="00")==1&&paidResults.Count(x=>x=="02")==5,"repeated concurrent IPN exactly once");
   mirrorUnavailable=true;try{await Sync(online);throw new Exception("Expected mirror outage");}catch(ApiException error)when(error.Status==503){}Check((await orderRepo.FactAsync(online,default)).WorkStatus=="pending_payment","outage cannot activate GPS prematurely");mirrorUnavailable=false;await Sync(online);await Sync(online);Check((await orderRepo.FactAsync(online,default)).WorkStatus=="pending","paid enters searching");Check(await Scalar(orderDb,"SELECT COUNT(*) FROM booking_dispatch WHERE booking_id=@id AND state='queued'",("id",online))==1,"one dispatch activation");Check(await Scalar(paymentDb,"SELECT COUNT(*) FROM payment_events WHERE booking_id=@id AND event_type='vnpay_paid'",("id",online))==1,"one revenue event");
   await Request("GET",$"/orders/{online}/status",404,actor:'E');await Request("GET",$"/orders/{online}/status",404,actor:'B');await Request("GET","/admin/payments",403);await Request("GET","/admin/payments",401,actor:null);
   var failed=await New("VNPAY");var ft=await Checkout(failed);
   await using(var qa=await new DatabaseConnectionFactory(Config(paymentDb)).OpenAsync()){await using var expire=qa.Sql("UPDATE payment_attempts SET expires_at=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE payment_id=(SELECT id FROM payments WHERE merchant_ref=@ref)",("ref",ft.Ref));await expire.ExecuteNonQueryAsync();}
   Check((await Request("GET",$"/orders/{failed}/status",200)).GetProperty("status").GetString()=="PENDING","expired browser session alone never asserts payment failure");
   Check(await Ipn(Callback(ft.Ref,"99","02"))=="00","provider ordinary failure");
   var cancelledAttempt=await Checkout(failed);Check(await Ipn(Callback(cancelledAttempt.Ref,"24","02"))=="00","user cancel records failure");Check((await Request("GET",$"/orders/{failed}/status",200)).GetProperty("status").GetString()=="FAILED","failed is not paid");var retry=await Checkout(failed);Check(retry.Ref!=ft.Ref,"retry new transaction same booking");Check(await Ipn(retry.Query)=="00","retry success");Check(await Ipn(Callback(ft.Ref,"99","02"))=="02","late failure cannot regress paid");
   var late=await New("VNPAY");var lt=await Checkout(late);await Work(late,"cancelled");await Sync(late);Check(await Ipn(lt.Query)=="00","late money after cancellation tracked");await Sync(late);Check((await Request("GET",$"/orders/{late}/status",200)).GetProperty("status").GetString()=="REFUND_PENDING","cancelled paid goes to refund pending, not GPS");
   var cash=await New("CASH");Check((await orderRepo.FactAsync(cash,default)).WorkStatus=="pending","cash starts searching unpaid");assignments[cash]=[new(1,1,21,"accepted"),new(2,2,22,"accepted")];await Request("GET",$"/orders/{cash}/status",200);await Sync(cash);await Request("POST","/cash/confirm-collection",409,new{orderId=cash},'B');
   await Work(cash,"completed");assignments[cash]=[new(1,1,21,"done"),new(2,2,22,"done")];var cs=await Request("GET",$"/orders/{cash}/status",200);Check(cs.GetProperty("status").GetString()=="UNPAID","completion not payment");
   await Request("POST","/cash/confirm-collection",403,new{orderId=cash},'C');await Task.WhenAll(Request("POST","/cash/confirm-collection",204,new{orderId=cash},'B'),Request("POST","/cash/confirm-collection",204,new{orderId=cash},'B'),Request("POST","/cash/confirm-collection",403,new{orderId=cash},'C'));Check((await Request("GET",$"/orders/{cash}/status",200)).GetProperty("status").GetString()=="CASH_AWAITING_CONFIRMATION","collector waits for customer");
   await Request("POST","/cash/customer-confirm",404,new{orderId=cash,paid=true},'E');await Request("POST","/cash/customer-confirm",204,new{orderId=cash,paid=true});await Request("POST","/cash/customer-confirm",204,new{orderId=cash,paid=true});Check(await Scalar(paymentDb,"SELECT COUNT(*) FROM payment_events WHERE booking_id=@id AND event_type='customer_paid'",("id",cash))==1,"cash duplicate confirmation counted once");
   Check(await Scalar(paymentDb,"SELECT COUNT(*) FROM payments WHERE booking_id=@id AND method='CASH' AND status='PAID'",("id",cash))==1,"one cash ledger transaction after repeated customer confirmation");
   await Request("POST",$"/admin/payments/{cash}/refund-request",204,new{evidence="QA assess full cash refund after completion"},'D');
   Check((await Request("GET",$"/orders/{cash}/status",200)).GetProperty("status").GetString()=="REFUND_PENDING","cash refund needs audited admin assessment");
   var dispute=await New("CASH");await Request("GET",$"/orders/{dispute}/status",200);assignments[dispute]=[new(3,1,21,"done"),new(4,2,22,"done")];await Work(dispute,"completed");await Sync(dispute);await Request("POST","/cash/confirm-collection",204,new{orderId=dispute},'B');await Request("POST","/cash/customer-confirm",204,new{orderId=dispute,paid=false});Check((await Request("GET",$"/orders/{dispute}/status",200)).GetProperty("status").GetString()=="DISPUTED","customer denies cash");await Request("POST",$"/admin/payments/{dispute}/resolve",204,new{paid=false,evidence="QA verified no cash received"},'D');
   await Request("POST",$"/admin/payments/{dispute}/collector",204,new{employeeId=2,evidence="QA collector replacement before payment"},'D');await Request("POST","/cash/confirm-collection",403,new{orderId=dispute},'B');await Request("POST","/cash/confirm-collection",204,new{orderId=dispute},'C');
   await Work(online,"cancelled");await Sync(online);var detail=await Request("GET",$"/admin/payments/{online}",200,actor:'D');var refundId=detail.GetProperty("history").GetProperty("refunds")[0].GetProperty("id").GetInt64();await Request("POST",$"/admin/payments/{online}/refunds/{refundId}/confirm",204,new{evidence="QA sandbox refund checked externally"},'D');await Request("POST",$"/admin/payments/{online}/refunds/{refundId}/confirm",204,new{evidence="QA sandbox refund checked externally"},'D');Check(await Ipn(tx.Query)=="02","old success after refund cannot regress status");Check((await Request("GET",$"/orders/{online}/status",200)).GetProperty("status").GetString()=="REFUNDED","refund never reopens order");
   var audit=await Request("GET","/admin/payments",200,actor:'D');Check(audit.GetProperty("summary")[0].GetProperty("orders").GetInt64()==5,"summary counts orders once, not IPN attempts");
   Check(await Scalar(orderDb,"SELECT COUNT(*) FROM bookings WHERE ABS(TIMESTAMPDIFF(SECOND,created_at,UTC_TIMESTAMP(6)))<60")==5,"new booking timestamps are UTC even with SYSTEM timezone");
   Check(await Scalar(paymentDb,"SELECT COUNT(*) FROM payments WHERE method='VNPAY' AND ABS(TIMESTAMPDIFF(SECOND,created_at,UTC_TIMESTAMP(6)))<60")>=3,"new transaction timestamps are UTC");
   using(var scope=app.Services.CreateScope()){
    var repo=scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
    Check(await repo.ArchiveSafe(online,true,true,default),"settled refunded order may be archived");
    Check(!await repo.ArchiveSafe(late,true,true,default),"pending late-payment refund blocks archive");
    Check(!await repo.ArchiveSafe(cash,true,false,default),"cash refund obligations block archive");
    Check(!await repo.ArchiveSafe(dispute,true,false,default),"unconfirmed cash blocks archive");
    Check(await repo.ReserveQuery(tx.Ref,default),"first query allowed independently of payment updated_at");
    Check(!await repo.ReserveQuery(tx.Ref,default),"query cooldown enforced");
    await using var qa=await new DatabaseConnectionFactory(Config(paymentDb)).OpenAsync();await using var elapsed=qa.Sql("UPDATE payment_attempts SET last_query_at=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 31 SECOND) WHERE payment_id=(SELECT id FROM payments WHERE merchant_ref=@ref)",("ref",tx.Ref));await elapsed.ExecuteNonQueryAsync();
    Check(await repo.ReserveQuery(tx.Ref,default),"query available after 30 seconds despite database timezone");
   }
   var encoded=VnPayGateway.Canonical(new Dictionary<string,string>{{"vnp_OrderInfo","a b&c"},{"vnp_Amount","100"}});Check(encoded=="vnp_Amount=100&vnp_OrderInfo=a+b%26c","provider sort and form encoding");
   var queryConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"VNPAY_TMN_CODE","QA123456"},{"VNPAY_HASH_SECRET",secret},{"VNPAY_PAYMENT_URL","https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"},{"VNPAY_RETURN_URL","https://qa.example.local/return"}}).Build();
   var invalidQueryHash=false;
   var gateway=new VnPayGateway(new VnPayOptions(queryConfig),new QueryClientFactory(async req=>{
    var fields=await req.Content!.ReadFromJsonAsync<Dictionary<string,string>>()??[];
    var canonical=string.Join('|',new[]{"vnp_RequestId","vnp_Version","vnp_Command","vnp_TmnCode","vnp_TxnRef","vnp_TransactionDate","vnp_CreateDate","vnp_IpAddr","vnp_OrderInfo"}.Select(name=>fields[name]));Check(fields["vnp_SecureHash"]==VnPayGateway.Sign(secret,canonical),"querydr request pipe signature");
    var response=new Dictionary<string,string>{{"vnp_ResponseId","query-qa"},{"vnp_Command","querydr"},{"vnp_ResponseCode","00"},{"vnp_Message","Success"},{"vnp_TmnCode","QA123456"},{"vnp_TxnRef",tx.Ref},{"vnp_Amount","10000000"},{"vnp_BankCode","NCB"},{"vnp_PayDate",DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss")},{"vnp_TransactionNo","123456789"},{"vnp_TransactionType","01"},{"vnp_TransactionStatus","00"},{"vnp_OrderInfo","QA"}};
    response["vnp_SecureHash"]=invalidQueryHash?"00":VnPayGateway.Sign(secret,string.Join('|',new[]{"vnp_ResponseId","vnp_Command","vnp_ResponseCode","vnp_Message","vnp_TmnCode","vnp_TxnRef","vnp_Amount","vnp_BankCode","vnp_PayDate","vnp_TransactionNo","vnp_TransactionType","vnp_TransactionStatus","vnp_OrderInfo"}.Select(name=>response[name])));
    return new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(response)};
   }));
   using(var scope=app.Services.CreateScope()){var attempt=await scope.ServiceProvider.GetRequiredService<IPaymentRepository>().Attempt(tx.Ref,default)??throw new Exception("Missing QA attempt");Check((await gateway.Query(attempt,default))?.TransactionStatus=="00","querydr verifies successful response");invalidQueryHash=true;try{await gateway.Query(attempt,default);throw new Exception("Accepted invalid querydr signature");}catch(ApiException e)when(e.Status==502){}}
   Console.WriteLine("PASS: payment HTTP auth, VNPay signature/amount/Return isolation, concurrent IPN, retries, cancellation/late callbacks, one GPS activation and no duplicate revenue.");
   Console.WriteLine("PASS: CASH completion remains unpaid; single authorized collector, concurrent collection, customer ownership/confirmation/dispute, audited admin reassignment and manual refund evidence.");
  }finally{if(app is not null){await app.StopAsync();await app.DisposeAsync();}if(source is not null){await source.StopAsync();await source.DisposeAsync();}foreach(var database in new[]{orderDb,paymentDb}){if(!Regex.IsMatch(database,"^cleanhome_test_pay_[a-f0-9]{32}_(order|payment)$"))throw new InvalidOperationException("Unsafe QA database");await using var drop=new MySqlCommand("DROP DATABASE IF EXISTS `"+database+"`",admin);await drop.ExecuteNonQueryAsync();}Console.WriteLine("Payment QA databases removed; no merchant credentials used.");}
 }
 private sealed class QueryClientFactory(Func<HttpRequestMessage,Task<HttpResponseMessage>> responder):IHttpClientFactory{
  public HttpClient CreateClient(string name)=>new(new QueryHandler(responder));
 }
 private sealed class QueryHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> responder):HttpMessageHandler{
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>responder(request);
 }
}
