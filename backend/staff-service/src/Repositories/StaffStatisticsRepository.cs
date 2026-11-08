using CleanHome.ServiceDefaults;
using CleanHome.ServiceDefaults.DTOs;
using CleanHome.Staff.Api.Interfaces;
namespace CleanHome.Staff.Api.Repositories;
public sealed class StaffStatisticsRepository(DatabaseConnectionFactory connections):IStaffStatisticsRepository
{
 public async Task<IReadOnlyList<StaffStatisticsAssignment>> GetAsync(long userId,CancellationToken ct)
 {
  await using var connection=await connections.OpenAsync(ct);
  await using var query=connection.Sql("""
   SELECT a.booking_id,a.employee_id,a.status,a.scheduled_start,
    (SELECT COUNT(*) FROM assignments p WHERE p.booking_id=a.booking_id AND p.status IN ('accepted','in_progress','done')),
    (SELECT COUNT(*) FROM assignments p WHERE p.booking_id=a.booking_id AND p.status IN ('accepted','in_progress','done') AND p.employee_id<a.employee_id),
    EXISTS(SELECT 1 FROM assignment_receipts r WHERE r.assignment_id=a.id),
    (SELECT x.source FROM assignment_exits x WHERE x.assignment_id=a.id ORDER BY x.id DESC LIMIT 1),
    EXISTS(SELECT 1 FROM replacement_assignments r WHERE r.assignment_id=a.id)
   FROM assignments a JOIN employees e ON e.id=a.employee_id WHERE e.user_id=@user ORDER BY a.booking_id
   """,("user",userId));
  var result=new List<StaffStatisticsAssignment>();
  await using var reader=await query.ExecuteReaderAsync(ct);
  while(await reader.ReadAsync(ct))result.Add(new(reader.GetInt64(0),reader.GetInt64(1),reader.GetString(2),reader.GetDateTime(3),Convert.ToInt32(reader.GetValue(4)),Convert.ToInt32(reader.GetValue(5)),reader.GetBoolean(6),reader.IsDBNull(7)?null:reader.GetString(7),reader.GetBoolean(8)));
  return result;
 }
}
