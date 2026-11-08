using System.Net;
using System.Net.Http.Json;
using CleanHome.Order.Api.Repositories;
using CleanHome.Order.Api.Models;
using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
internal static class LifecycleArchiveChecks{
 public static async Task RunAsync(string connection,long cancelled,long active,long actor){
  if(!new MySqlConnectionStringBuilder(connection).Database.StartsWith("cleanhome_test_",StringComparison.Ordinal))throw new Exception("QA database required");
  var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:Database",connection},{"InternalApi:Key","qa-only"},{"Services:PaymentUrl","http://qa-payments.local"}}).Build();
  var factory=new DatabaseConnectionFactory(config);var clients=new ClientFactory();var repository=new OrderArchiveRepository(factory,clients,config);var orders=new CustomerOrderRepository(factory);
  async Task Reject(Func<Task<object>> action,int status){try{await action();throw new Exception("Unexpected archive success");}catch(ApiException e)when(e.Status==status){}}
  var request=new ArchiveRequest{Reason="QA soft delete with audited reason"};
  await Reject(()=>repository.Change(active,actor,true,request,default),409);
  clients.Safe=false;await Reject(()=>repository.Change(cancelled,actor,true,request,default),409);
  clients.Fail=true;await Reject(()=>repository.Change(cancelled,actor,true,request,default),503);clients.Fail=false;clients.Safe=true;
  await repository.Change(cancelled,actor,true,request,default);await repository.Change(cancelled,actor,true,request,default);
  var normal=await orders.ListAsync(null,new OrderFilter(1,1000,null,null,null,null,null,AdminOnly:true),default);var trash=await orders.ListAsync(null,new OrderFilter(1,1000,null,null,null,null,null,true,true),default);
  if(normal.Items.Any(b=>b.Id==cancelled)||!trash.Items.Any(b=>b.Id==cancelled))throw new Exception("Archive default/trash filter failed");
  var detail=await orders.FindAsync(null,cancelled,default)??throw new Exception("Archive erased booking");if(detail.Summary.Status!="cancelled")throw new Exception("Archive changed work status");
  var customer=await orders.ListAsync(detail.CustomerId,new OrderFilter(1,1000,null,null,null,null,null),default);if(!customer.Items.Any(b=>b.Id==cancelled))throw new Exception("Admin archive erased customer history");
  await repository.Change(cancelled,actor,false,new ArchiveRequest{Reason="QA restore without reopening"},default);await repository.Change(cancelled,actor,false,new ArchiveRequest{Reason="QA restore without reopening"},default);
  if((await orders.FindAsync(null,cancelled,default))?.Summary.Status!="cancelled")throw new Exception("Restore reactivated cancelled work");
  await using var c=await factory.OpenAsync();await using var q=c.Sql("SELECT COUNT(*) FROM order_management_audit WHERE booking_id=@id",("id",cancelled));if(Convert.ToInt64(await q.ExecuteScalarAsync())!=2)throw new Exception("Repeated archive/restore duplicated audit");
  Console.WriteLine("PASS: soft delete/restore, admin-only display filtering, preserved customer/financial history, obligation/outage guards and duplicate-action audit.");
 }
 private sealed class ClientFactory:IHttpClientFactory{public bool Safe=true,Fail;public HttpClient CreateClient(string name)=>new(new Handler(this));}
 private sealed class Handler(ClientFactory owner):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(owner.Fail?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK){Content=JsonContent.Create(owner.Safe)});}
}
