using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ssamc.DB.Entities;

/// <summary>
/// 模块权限点
/// </summary>
[Table("SYS_MODULE_RIGHT")]
public partial class SYS_MODULE_RIGHT
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
    /// 权限点
    /// </summary>
    [Precision(19)]
    public long? ID_RIGHT { get; set; }

    /// <summary>
    /// 功能编码
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_CODE { get; set; }

    /// <summary>
    /// 中文名称
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_NAME { get; set; }

    /// <summary>
    /// 英文名称
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_NAMEEN { get; set; }

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
