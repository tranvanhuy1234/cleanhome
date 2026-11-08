using CleanHome.ServiceDefaults;
using CleanHome.User.Api.Interfaces;
using CleanHome.User.Api.Models;

namespace CleanHome.User.Api.Repositories;

public sealed class ProfileRepository(DatabaseConnectionFactory connections) : IProfileRepository
{
    public async Task<UserProfile?> FindAsync(long userId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql("SELECT user_id,full_name,phone,avatar_url,created_at,updated_at FROM profiles WHERE user_id=@id", ("id", userId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new UserProfile(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetDateTime(4), reader.GetDateTime(5)) : null;
    }

    public async Task<UserProfile?> UpdateAsync(UserProfile profile, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var find = connection.Sql("SELECT user_id FROM profiles WHERE user_id=@id FOR UPDATE", ("id", profile.UserId));
        find.Transaction = transaction;
        if (await find.ExecuteScalarAsync(ct) is null) return null;

        await using var update = connection.Sql("UPDATE profiles SET full_name=@name,phone=@phone,avatar_url=@avatar,updated_at=UTC_TIMESTAMP(6) WHERE user_id=@id",
            ("id", profile.UserId), ("name", profile.FullName), ("phone", profile.Phone), ("avatar", profile.AvatarUrl));
        update.Transaction = transaction;
        await update.ExecuteNonQueryAsync(ct);

        // Đọc kết quả trong cùng transaction để response khớp đúng bản ghi vừa lưu.
        await using var read = connection.Sql("SELECT user_id,full_name,phone,avatar_url,created_at,updated_at FROM profiles WHERE user_id=@id", ("id", profile.UserId));
        read.Transaction = transaction;
        UserProfile result;
        await using (var reader = await read.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            result = new UserProfile(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetDateTime(4), reader.GetDateTime(5));
        }
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task SaveAsync(UserProfile profile, bool createOnly, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        // READ COMMITTED + row lock: kiểm tra tồn tại và update trong cùng transaction.
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await using var find = connection.Sql("SELECT user_id FROM profiles WHERE user_id=@id FOR UPDATE", ("id", profile.UserId));
        find.Transaction = transaction;
        var exists = await find.ExecuteScalarAsync(ct) is not null;
        if (!exists || !createOnly)
        {
            await using var command = connection.Sql(exists
                ? "UPDATE profiles SET full_name=@name,phone=@phone,avatar_url=@avatar,updated_at=UTC_TIMESTAMP(6) WHERE user_id=@id"
                : "INSERT INTO profiles (user_id,full_name,phone,avatar_url) VALUES (@id,@name,@phone,@avatar)",
                ("id", profile.UserId), ("name", profile.FullName), ("phone", profile.Phone), ("avatar", profile.AvatarUrl));
            command.Transaction = transaction;
            try { await command.ExecuteNonQueryAsync(ct); }
            catch (MySqlConnector.MySqlException error) when (error.Number == 1062 && createOnly)
            {
                // Một worker khác có thể đã provision cùng user. Không ghi đè hồ sơ đã sửa.
                if (await find.ExecuteScalarAsync(ct) is null) throw;
            }
        }
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<Address>> AddressesAsync(long userId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql("SELECT id,user_id,recipient,phone,address_text,latitude,longitude FROM addresses WHERE user_id=@id ORDER BY id DESC", ("id", userId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<Address>();
        while (await reader.ReadAsync(ct)) items.Add(new Address(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetDecimal(6)));
        return items;
    }

    public async Task<long> AddAddressAsync(Address address, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql("INSERT INTO addresses (user_id,recipient,phone,address_text,latitude,longitude) VALUES (@user,@recipient,@phone,@text,@lat,@lng)",
            ("user", address.UserId), ("recipient", address.Recipient), ("phone", address.Phone), ("text", address.AddressText), ("lat", address.Latitude), ("lng", address.Longitude));
        await command.ExecuteNonQueryAsync(ct);
        return command.LastInsertedId;
    }

    public async Task<bool> UpdateAddressAsync(Address address, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql("UPDATE addresses SET recipient=@recipient,phone=@phone,address_text=@text,latitude=@lat,longitude=@lng WHERE id=@id AND user_id=@user",
            ("id", address.Id), ("user", address.UserId), ("recipient", address.Recipient), ("phone", address.Phone), ("text", address.AddressText), ("lat", address.Latitude), ("lng", address.Longitude));
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAddressAsync(long userId, long addressId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await using var command = connection.Sql("DELETE FROM addresses WHERE id=@id AND user_id=@user", ("id", addressId), ("user", userId));
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }
}
