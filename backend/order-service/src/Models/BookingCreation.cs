namespace CleanHome.Order.Api.Models;
public sealed record BookingService(long Id,string Name,string Unit,decimal BasePrice,int DurationMinutes);
public sealed record BookingAddress(long Id,string Recipient,string Phone,string AddressText,decimal Latitude,decimal Longitude);
public sealed record BookingBranch(long Id,string Name,string AddressText,string Status);
public sealed record BookingCreation(long CustomerId,string Key,string Hash,long BranchId,BookingService Service,BookingAddress Address,decimal Quantity,DateTime Start,DateTime End,string? Note,int RequiredStaff=1,bool AutomaticDispatch=false,string Area="Khu vực chi nhánh",string? PaymentMethod=null);
public sealed record BookingCreated(long Id,string Code,string Status,decimal TotalAmount,DateTime ScheduledStart,DateTime ScheduledEnd);
