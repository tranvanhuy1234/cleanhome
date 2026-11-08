using CleanHome.Order.Api.DTOs.Requests;
using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.Order.Api.Services;
using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
internal static class StaffStatisticsChecks
{
 public static async Task RunAsync()
 {
  var source=new Source();var repository=new Orders();var service=new StaffStatisticsService(source,repository);
  var query=new StaffStatisticsQuery{From=new(2026,11,1),To=new(2026,11,30),GroupBy="day",PageSize=1};
  var shares=new List<decimal>();
  for(var i=0;i<3;i++){source.Index=i;var result=await service.GetAsync(100+i,query,default);shares.Add(result.Summary.AllocatedRevenue);Check(result.History.Total==1&&result.Revenue.Sum(p=>p.Revenue)==result.Summary.AllocatedRevenue,"chart/history/summary sync");Check(result.History.Items[0].CompletedAt==new DateTime(2026,10,31,18,0,0,DateTimeKind.Utc),"Vietnam date boundary uses completion, not assignment date");}
  Check(shares.Sum()==100.01m&&shares.SequenceEqual(new[]{33.34m,33.34m,33.33m}),"exact remainder allocation");
  Check((await service.GetAsync(100,query,default)).Summary.Received==2,"accepted then cancelled still counts as received");
  source.Index=0;query.From=new(2026,10,1);query.To=new(2026,10,31);var october=await service.GetAsync(100,query,default);Check(october.Summary.AllocatedRevenue==0,"UTC October completion belongs to November in Vietnam");
  query.From=new(2026,11,1);query.To=new(2026,11,30);query.GroupBy="week";var weeks=await service.GetAsync(100,query,default);Check(weeks.Revenue.Sum(p=>p.Revenue)==33.34m,"weekly buckets preserve total");
  query.GroupBy="month";Check((await service.GetAsync(100,query,default)).Revenue.Count==1,"monthly grouping");
  query.To=new(2028,1,1);try{await service.GetAsync(100,query,default);throw new Exception("Expected range rejection");}catch(ApiException e)when(e.Status==400){}
  Console.WriteLine("PASS: statistics exact equal split/remainder, duplicate prevention, cancellation exclusion, completion-date timezone and chart grouping.");
 }
 static void Check(bool condition,string description){if(!condition)throw new Exception(description);}
 sealed class Source:IStaffStatisticsClient
 {
  public int Index{get;set;}
  public Task<IReadOnlyList<StaffStatisticsAssignment>> GetAsync(long userId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<StaffStatisticsAssignment>>([new(1,Index+1,"done",new(2026,10,1),3,Index),new(1,Index+1,"done",new(2026,10,1),3,Index),new(2,Index+1,"cancelled",new(2026,11,10),3,Index,true),new(3,Index+1,"waiting",new(2026,11,10),0,0)]);
 }
 sealed class Orders:IStaffStatisticsOrderRepository
 {
  public Task<IReadOnlyList<StaffStatisticsOrder>> GetAsync(IEnumerable<long> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<StaffStatisticsOrder>>([new(1,"STAT-1","completed",100.01m,new(2026,10,31,18,0,0,DateTimeKind.Utc),"QA service"),new(2,"STAT-2","cancelled",999m,null,"Cancelled QA"),new(3,"STAT-3","confirmed",999m,null,"Waiting QA")]);
 }
}
