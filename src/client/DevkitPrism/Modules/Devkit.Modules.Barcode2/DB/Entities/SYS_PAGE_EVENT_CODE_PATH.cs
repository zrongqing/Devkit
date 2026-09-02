using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Barcode2.DB.Entities;

/// <summary>
/// 事件附加代码历史
/// </summary>
[Table("SYS_PAGE_EVENT_CODE_PATH")]
public class SYS_PAGE_EVENT_CODE_PATH
{
    /// <summary>
    /// ID
    /// </summary>
    [Key]
    [StringLength(32)]
    [Unicode(false)]
    public long? ID { get; set; } = null!;

    /// <summary>
    /// 名称
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_NAME { get; set; }

    /// <summary>
    /// 引用文件
    /// </summary>
    [StringLength(3000)]
    [Unicode(false)]
    public string? STR_USING { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(255)]
    [Unicode(false)]
    public string? STR_NOTES { get; set; }

    /// <summary>
    /// 有修改
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_MODIFY { get; set; }

    /// <summary>
    /// 单据状态
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_STATE { get; set; }

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
    /// 操作日期
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? DT_UP { get; set; }

    /// <summary>
    /// 生成日期
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? DT_CREATE { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    [Precision(19)]
    public long? ID_MODULE { get; set; }

    /// <summary>
    /// 页面名称
    /// </summary>
    [Precision(19)]
    public long? ID_PAGE { get; set; }

    /// <summary>
    /// 列表名称
    /// </summary>
    [Precision(19)]
    public long? ID_PAGE_LIST { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_CODE { get; set; }

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
    /// 扩展源代码
    /// </summary>
    [Column(TypeName = "NCLOB")]
    public string? STR_EXTEND { get; set; }

    /// <summary>
    /// 源代码
    /// </summary>
    [Column(TypeName = "NCLOB")]
    public string? STR_SOURCE { get; set; }

    /// <summary>
    /// 详细设计
    /// </summary>
    [Column(TypeName = "NCLOB")]
    public string? STR_DETAIL { get; set; }

    /// <summary>
    /// 扩展代码
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_EXTEND { get; set; }

    /// <summary>
    /// 事件
    /// </summary>
    [StringLength(32)]
    [Unicode(false)]
    public string? ID_EVENT { get; set; }

    /// <summary>
    /// 待完成
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_WAIT { get; set; }

    /// <summary>
    /// 代码ID
    /// </summary>
    [Precision(19)]
    public long? ID_CODE { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    [Precision(19)]
    public long? ID_BY { get; set; }

    /// <summary>
    /// ID
    /// </summary>
    [Precision(19)]
    public long? ID_ { get; set; }

    /// <summary>
    /// 编译引用
    /// </summary>
    [StringLength(200)]
    [Unicode(false)]
    public string? STR_REFERENCE { get; set; }
}
