namespace Ssamc.DB.Entities.Common;

public abstract class EntityBase
{
    public long Id { get; set; }
    public int? IS_DELETE { get; set; }
}
