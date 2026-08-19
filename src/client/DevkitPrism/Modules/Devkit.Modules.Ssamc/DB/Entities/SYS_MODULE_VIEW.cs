using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ssamc.DB.Entities;

/// <summary>
/// 模块所用视图
/// </summary>
[Table("SYS_MODULE_VIEW")]
public class SYS_MODULE_VIEW
{
    /// <summary>
    /// ID
    /// </summary>
    [Key]
    [Precision(19)]
    public long ID { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    [Precision(19)]
    public long? ID_MODULE { get; set; }

    /// <summary>
    /// 视图
    /// </summary>
    [Precision(19)]
    public long? ID_VIEW { get; set; }

    /// <summary>
    /// 中文名称
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_NAME { get; set; }

    /// <summary>
    /// 英文名称
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_NAMEEN { get; set; }

    /// <summary>
    /// 查询条件
    /// </summary>
    [StringLength(500)]
    [Unicode(false)]
    public string? STR_WHERE { get; set; }

    /// <summary>
    /// 参数传值
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_PARAM { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [Precision(5)]
    public short? INT_SORT { get; set; }

    /// <summary>
    /// 更新状态
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_STATE { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    [Precision(19)]
    public long? ID_BY { get; set; }

    /// <summary>
    /// 操作日期
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? DT_UP { get; set; }

    /// <summary>
    /// 删除标记
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_DELETE { get; set; }

    /// <summary>
    /// 处理标志
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_DO { get; set; }

    /// <summary>
    /// 有修改
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_MODIFY { get; set; }
}
