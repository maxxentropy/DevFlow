namespace DevFlow.SharedKernel.Entities;

/// <summary>
/// Interface for entities that support audit tracking.
/// </summary>
public interface IAuditable
{
  /// <summary>
  /// Gets the date and time when the entity was created.
  /// </summary>
  DateTimeOffset CreatedAt { get; }

  /// <summary>
  /// Gets the identifier of the user who created the entity.
  /// </summary>
  string? CreatedBy { get; }

  /// <summary>
  /// Gets the date and time when the entity was last modified.
  /// </summary>
  DateTimeOffset? LastModifiedAt { get; }

  /// <summary>
  /// Gets the identifier of the user who last modified the entity.
  /// </summary>
  string? LastModifiedBy { get; }
}

/// <summary>
/// Base implementation of auditable entity.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditable
{
  /// <summary>
  /// Gets or sets the date and time when the entity was created.
  /// </summary>
  public DateTimeOffset CreatedAt { get; protected set; }

  /// <summary>
  /// Gets or sets the identifier of the user who created the entity.
  /// </summary>
  public string? CreatedBy { get; protected set; }

  /// <summary>
  /// Gets or sets the date and time when the entity was last modified.
  /// </summary>
  public DateTimeOffset? LastModifiedAt { get; protected set; }

  /// <summary>
  /// Gets or sets the identifier of the user who last modified the entity.
  /// </summary>
  public string? LastModifiedBy { get; protected set; }

  /// <summary>
  /// Initializes a new instance of the <see cref="AuditableEntity"/> class.
  /// </summary>
  protected AuditableEntity()
  {
    CreatedAt = DateTimeOffset.UtcNow;
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="AuditableEntity"/> class with the specified identifier.
  /// </summary>
  /// <param name="id">The entity identifier</param>
  protected AuditableEntity(Guid id) : base(id)
  {
    CreatedAt = DateTimeOffset.UtcNow;
  }

  /// <summary>
  /// Sets the audit information for entity creation.
  /// </summary>
  /// <param name="createdBy">The user who created the entity</param>
  /// <param name="createdAt">The date and time when the entity was created</param>
  public virtual void SetCreationAudit(string? createdBy, DateTimeOffset? createdAt = null)
  {
    CreatedBy = createdBy;
    CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
  }

  /// <summary>
  /// Sets the audit information for entity modification.
  /// </summary>
  /// <param name="modifiedBy">The user who modified the entity</param>
  /// <param name="modifiedAt">The date and time when the entity was modified</param>
  public virtual void SetModificationAudit(string? modifiedBy, DateTimeOffset? modifiedAt = null)
  {
    LastModifiedBy = modifiedBy;
    LastModifiedAt = modifiedAt ?? DateTimeOffset.UtcNow;
  }
}