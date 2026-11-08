using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.Order.Api.Repositories;
using CleanHome.Order.Api.Services;
using CleanHome.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

internal static class BookingCreationChecks
{
 public static async Task RunAsync(string root,string adminConnection)
 {
  var database="cleanhome_test_booking_"+Guid.NewGuid().ToString("N");
  await using var admin=new MySqlConnection(adminConnection);await admin.OpenAsync();
  try{
   foreach(var file in new[]{"001_initial.sql","003_booking_creation.sql","004_dispatch.sql","005_payments.sql","006_order_lifecycle.sql"}){
    var sql=(await File.ReadAllTextAsync(Path.Combine(root,"databases/order-db",file))).Replace("cleanhome_order",database,StringComparison.Ordinal);
    await using var create=new MySqlCommand(sql,admin);await create.ExecuteNonQueryAsync();
   }
   var connectionString=new MySqlConnectionStringBuilder(adminConnection){Database=database}.ConnectionString;
   var factory=new DatabaseConnectionFactory(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:Database",connectionString}}).Build());
   var repository=new BookingCreationRepository(factory);var dependencies=new Dependencies();var service=new BookingCreationService(repository,dependencies);
   CreateBookingRequest Request(string key)=>new(){IdempotencyKey=key,ServiceId=1,AddressId=1,BranchId=1,Quantity=2,ScheduledStart=new DateTimeOffset(DateTime.UtcNow.Date.AddDays(2).AddHours(2)),SpecialRequest="Kiểm thử"};
   var input=Request("booking-success-01");var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>service.CreateAsync(10,"token",input,default)));
   Check(results.Select(r=>r.Id).Distinct().Count()==1,"concurrent retry creates exactly one booking");
   Check(results.All(r=>r.TotalAmount==150000&&r.Status=="pending"),"server price and pending state");
   await using var db=new MySqlConnection(connectionString);await db.OpenAsync();
   async Task<long> Count(string table){await using var q=new MySqlCommand("SELECT COUNT(*) FROM "+table,db);return Convert.ToInt64(await q.ExecuteScalarAsync());}
   Check(await Count("bookings")==1&&await Count("booking_items")==1&&await Count("booking_status_history")==1,"atomic booking/item/history and no duplicates");
   dependencies.Unavailable=true;
   Check((await service.CreateAsync(10,"token",input,default)).Id==results[0].Id,"committed retry works while dependencies unavailable");dependencies.Unavailable=false;
   var conflict=Request(input.IdempotencyKey);conflict.Quantity=3;await Reject(()=>service.CreateAsync(10,"token",conflict,default),409);
   var past=Request("booking-past-01");past.ScheduledStart=DateTimeOffset.UtcNow.AddHours(-1);await Reject(()=>service.CreateAsync(10,"token",past,default),400);
   var fractional=Request("booking-fraction-01");fractional.Quantity=1.5m;await Reject(()=>service.CreateAsync(10,"token",fractional,default),400);
   var foreign=Request("booking-foreign-01");foreign.AddressId=999;await Reject(()=>service.CreateAsync(10,"token",foreign,default),404);
   var inactive=Request("booking-inactive-01");inactive.ServiceId=999;await Reject(()=>service.CreateAsync(10,"token",inactive,default),404);
   var branch=Request("booking-branch-01");branch.BranchId=999;await Reject(()=>service.CreateAsync(10,"token",branch,default),409);
   var late=Request("booking-late-01");late.ScheduledStart=new DateTimeOffset(DateTime.UtcNow.Date.AddDays(2).AddHours(10));await Reject(()=>service.CreateAsync(10,"token",late,default),400);
   var duration=Request("booking-long-01");duration.Quantity=100;await Reject(()=>service.CreateAsync(10,"token",duration,default),400);
   Check(await Count("bookings")==1,"rejected requests leave no partial bookings");
   var other=await service.CreateAsync(11,"other-token",input,default);Check(other.Id!=results[0].Id,"idempotency key scoped to customer");
   var automatic=Request("automatic-confirmed-01");automatic.AutomaticDispatch=true;automatic.RequiredStaff=3;automatic.Location=new(){Latitude=10.779m,Longitude=106.701m,AddressText="Địa chỉ ghim đã xác nhận",Confirmed=true};
   var autoOrder=await service.CreateAsync(10,"token",automatic,default);
   await using(var queued=db.Sql("SELECT required_staff FROM booking_dispatch WHERE booking_id=@id AND state='queued'",("id",autoOrder.Id)))Check(Convert.ToInt32(await queued.ExecuteScalarAsync())==3,"booking and durable recruitment queue commit together");
   var unconfirmed=Request("automatic-unconfirmed-01");unconfirmed.AutomaticDispatch=true;unconfirmed.Location=new(){Latitude=10,Longitude=106,AddressText="Unconfirmed address",Confirmed=false};await Reject(()=>service.CreateAsync(10,"token",unconfirmed,default),400);
   var online=Request("booking-vnpay-method-01");online.PaymentMethod="VNPAY";online.AutomaticDispatch=true;online.Location=automatic.Location;
   var held=await service.CreateAsync(10,"token",online,default);Check(held.Status=="pending_payment","VNPay creates pending payment work state");
   await using(var check=db.Sql("SELECT COUNT(*) FROM booking_dispatch d JOIN booking_payment_state p ON p.booking_id=d.booking_id WHERE d.booking_id=@id AND d.state='awaiting_payment' AND p.method='VNPAY' AND p.status='PENDING'",("id",held.Id)))Check(Convert.ToInt32(await check.ExecuteScalarAsync())==1,"online payment and held GPS commit atomically");
   online.PaymentMethod="CASH";await Reject(()=>service.CreateAsync(10,"token",online,default),409);
   online.IdempotencyKey="booking-cash-method-01";var cash=await service.CreateAsync(10,"token",online,default);Check(cash.Status=="pending","cash searches before collecting");
   await using(var check=db.Sql("SELECT COUNT(*) FROM booking_dispatch d JOIN booking_payment_state p ON p.booking_id=d.booking_id WHERE d.booking_id=@id AND d.state='queued' AND p.method='CASH' AND p.status='UNPAID'",("id",cash.Id)))Check(Convert.ToInt32(await check.ExecuteScalarAsync())==1,"cash unpaid and GPS queue commit together");
   Console.WriteLine("PASS: booking price/snapshots/atomic writes, concurrent idempotency, replay during outage, ownership, inactive references, time/quantity bounds and customer key isolation.");
  }finally{
   if(!System.Text.RegularExpressions.Regex.IsMatch(database,"^cleanhome_test_booking_[a-f0-9]{32}$"))throw new InvalidOperationException("Unsafe QA database name.");
   await using var cleanup=new MySqlCommand($"DROP DATABASE IF EXISTS `{database}`",admin);await cleanup.ExecuteNonQueryAsync();Console.WriteLine("Booking QA database removed.");
  }
 }
 static void Check(bool valid,string description){if(!valid)throw new InvalidOperationException(description);}
 static async Task Reject(Func<Task<BookingCreated>> action,int status){try{await action();throw new InvalidOperationException("Expected rejection "+status);}catch(ApiException e)when(e.Status==status){}}
 sealed class Dependencies:IBookingDependencies
 {
  public bool Unavailable{get;set;}
  public Task<BookingService> ServiceAsync(long id,CancellationToken ct){if(Unavailable)throw new ApiException(503,"Unavailable");if(id!=1)throw new ApiException(404,"Inactive");return Task.FromResult(new BookingService(1,"Dịch vụ test","giờ",75000,60));}
  public Task<BookingAddress> AddressAsync(long id,string token,CancellationToken ct){if(id!=1)throw new ApiException(404,"Foreign address");return Task.FromResult(new BookingAddress(1,"Khách QA","0901234567","Địa chỉ QA",10,106));}
  public Task<IReadOnlyList<BookingBranch>> BranchesAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<BookingBranch>>([new(1,"QA branch","Địa chỉ QA","active")]);
 }
}
