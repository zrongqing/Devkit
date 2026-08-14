using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ssamc.DB.Entities;

/// <summary>
/// 菜单分组
/// </summary>
[Table("SYS_MENU_GROUP")]
public partial class SYS_MENU_GROUP
{
    /// <summary>
    /// ID
    /// </summary>
    [Key]
    [Precision(19)]
    public long ID { get; set; }

    /// <summary>
    /// 上级 目录
    /// </summary>
    [Precision(19)]
    public long? ID_TOP { get; set; }

    /// <summary>
    /// 菜单编码
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
    /// 菜单图标
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_ICON { get; set; }

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
    /// 有修改
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_MODIFY { get; set; }

    /// <summary>
    /// 处理标记
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_DO { get; set; }
}
