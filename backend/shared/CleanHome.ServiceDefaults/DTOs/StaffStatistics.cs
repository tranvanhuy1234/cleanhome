namespace CleanHome.ServiceDefaults.DTOs;
public sealed record StaffStatisticsAssignment(long BookingId,long EmployeeId,string Status,DateTime ScheduledStart,int ParticipantCount,int ShareIndex,bool HasAccepted=false,string? ExitSource=null,bool Replacement=false);
