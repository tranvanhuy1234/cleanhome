namespace CleanHome.Auth.Api.Interfaces;
public interface IActiveStaffRepository{Task<IReadOnlyList<long>> GetAsync(long[] ids,CancellationToken ct);}
