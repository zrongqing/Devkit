using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SSAMC.DB.Entities;

/// <summary>
/// 事件附加代码
/// </summary>
[Table("SYS_PAGE_EVENT_CODE_BACK2")]
public partial class SYS_PAGE_EVENT_CODE_BACK2
{
    /// <summary>
    /// 页面名称
    /// </summary>
    [StringLength(32)]
    [Unicode(false)]
    public string? ID_PAGE { get; set; }

    /// <summary>
    /// 源代码
    /// </summary>
    [Column(TypeName = "NCLOB")]
    public string? STR_SOURCE { get; set; }

    /// <summary>
    /// 事件
    /// </summary>
    [Key]
    [Precision(19)]
    public long ID { get; set; }

    /// <summary>
    /// 开发项目
    /// </summary>
    [Precision(19)]
    public long? ID_PROJECT { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    [Precision(19)]
    public long? ID_MODULE { get; set; }

    /// <summary>
    /// 列表名称
    /// </summary>
    [Column(TypeName = "NUMBER(32)")]
    public decimal? ID_PAGE_LIST { get; set; }

    /// <summary>
    /// 事件
    /// </summary>
    [Precision(19)]
    public long? ID_EVENT { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_CODE { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_NAME { get; set; }

    /// <summary>
    /// 引用 文件
    /// </summary>
    [StringLength(3000)]
    [Unicode(false)]
    public string? STR_USING { get; set; }

    /// <summary>
    /// 扩展源代码
    /// </summary>
    [Column(TypeName = "NCLOB")]
    public string? STR_EXTEND { get; set; }

    /// <summary>
    /// 详细设计
    /// </summary>
    [Column(TypeName = "NCLOB")]
    public string? STR_DETAIL { get; set; }

    /// <summary>
    /// 数据库
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? STR_DB { get; set; }

    /// <summary>
    /// 打包编译
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_PACK { get; set; }

    /// <summary>
    /// 待完成
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_WAIT { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(255)]
    [Unicode(false)]
    public string? STR_NOTES { get; set; }

    /// <summary>
    /// 单据状态
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_STATE { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    [Precision(19)]
    public long? ID_BY { get; set; }

    /// <summary>
    /// 生成日期
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? DT_CREATE { get; set; }

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

    /// <summary>
    /// 升级状态
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_UPGRADE { get; set; }

    /// <summary>
    /// 目标日期
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? DT_UPGRADE { get; set; }

    /// <summary>
    /// 忽略升级
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_IGNORE { get; set; }

    /// <summary>
    /// 最后执行时间
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? DT_EXEC { get; set; }

    /// <summary>
    /// 执行时长
    /// </summary>
    [Precision(10)]
    public int? DBL_EXEC { get; set; }

    /// <summary>
    /// 源代码(VUE)
    /// </summary>
    [Column(TypeName = "NCLOB")]
    public string? STR_SOURCE_VUE { get; set; }

    /// <summary>
    /// 扩展源代码(VUE)
    /// </summary>
    [Column(TypeName = "NCLOB")]
    public string? STR_EXTEND_VUE { get; set; }

    /// <summary>
    /// 已编译
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_COMPILE { get; set; }

    /// <summary>
    /// 编译错误
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_ERROR { get; set; }

    /// <summary>
    /// 数据库
    /// </summary>
    [Precision(19)]
    public long? ID_DB { get; set; }
}
