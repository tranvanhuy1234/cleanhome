using CleanHome.Auth.Api.Interfaces;
using CleanHome.ServiceDefaults;
namespace CleanHome.Auth.Api.Repositories;
public sealed class ActiveStaffRepository(DatabaseConnectionFactory connections):IActiveStaffRepository
{
 public async Task<IReadOnlyList<long>> GetAsync(long[] ids,CancellationToken ct)
 {
  if(ids.Length==0)return [];
  await using var c=await connections.OpenAsync(ct);var values=ids.Distinct().Select((id,i)=>(Name:"p"+i,Value:(object?)id)).ToArray();
  await using var q=c.Sql($"SELECT id FROM accounts WHERE role='staff' AND status='active' AND (locked_until IS NULL OR locked_until<=UTC_TIMESTAMP(6)) AND id IN ({string.Join(',',values.Select(v=>"@"+v.Name))})",values);
  var result=new List<long>();await using var reader=await q.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))result.Add(reader.GetInt64(0));return result;
 }
}
