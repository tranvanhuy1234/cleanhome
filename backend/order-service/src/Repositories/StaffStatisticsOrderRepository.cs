using CleanHome.Order.Api.Interfaces;
using CleanHome.Order.Api.Models;
using CleanHome.ServiceDefaults;
namespace CleanHome.Order.Api.Repositories;
public sealed class StaffStatisticsOrderRepository(DatabaseConnectionFactory connections):IStaffStatisticsOrderRepository
{
 public async Task<IReadOnlyList<StaffStatisticsOrder>> GetAsync(IEnumerable<long> ids,CancellationToken ct)
 {
  var result=new List<StaffStatisticsOrder>();
  await using var connection=await connections.OpenAsync(ct);
  await using var transaction=await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
  foreach(var chunk in ids.Distinct().Chunk(200)){
   var values=chunk.Select((id,index)=>(Name:"id"+index,Value:(object?)id)).ToArray();
   await using var query=connection.Sql($"""
    SELECT b.id,b.code,b.status,b.total_amount,
    (SELECT MAX(h.created_at) FROM booking_status_history h WHERE h.booking_id=b.id AND h.status='completed'),
    COALESCE((SELECT GROUP_CONCAT(i.service_name_snapshot ORDER BY i.id SEPARATOR ', ') FROM booking_items i WHERE i.booking_id=b.id),'')
    FROM bookings b WHERE b.id IN ({string.Join(",",values.Select(v=>"@"+v.Name))}) ORDER BY b.id
    """,values);query.Transaction=transaction;
   await using var reader=await query.ExecuteReaderAsync(ct);
   while(await reader.ReadAsync(ct))result.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetDecimal(3),reader.IsDBNull(4)?null:reader.GetDateTime(4),reader.GetString(5)));
  }
  await transaction.CommitAsync(ct);return result;
 }
}
