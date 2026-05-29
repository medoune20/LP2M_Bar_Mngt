using LP2M_Bar_Mngt.Domain.Common;

namespace LP2M_Bar_Mngt.Domain.Entities;

public sealed class Role : Entity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
}

public sealed class User : Entity
{
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public long RoleId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
}

public sealed class AuditLog : Entity
{
    public long? UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public long? EntityId { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
}
