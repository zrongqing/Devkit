using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ssamc.DB.Entities;

/// <summary>
/// 菜单
/// </summary>
[Table("SYS_MENU")]
public class SYS_MENU
{
    /// <summary>
    /// ID
    /// </summary>
    [Key]
    [Precision(19)]
    public long ID { get; set; }

    /// <summary>
    /// 上级目录
    /// </summary>
    [Precision(19)]
    public long? ID_TOP { get; set; }

    /// <summary>
    /// 开发项目
    /// </summary>
    [Precision(19)]
    public long? ID_PROJECT { get; set; }

    /// <summary>
    /// 菜单分组
    /// </summary>
    [Precision(19)]
    public long? ID_GROUP { get; set; }

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
    /// 菜单类型
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_TYPE { get; set; }

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
    /// 按钮图标
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_ICON { get; set; }

    /// <summary>
    /// 展开状态
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_EXPAND { get; set; }

    /// <summary>
    /// 主目录
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_MAIN { get; set; }

    /// <summary>
    /// 是节点
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_NODE { get; set; }

    /// <summary>
    /// 系统菜单
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_SYS { get; set; }

    /// <summary>
    /// 适用平台
    /// </summary>
    [StringLength(10)]
    [Unicode(false)]
    public string? STR_FOR { get; set; }

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
    public short? IS_DELETE { get; set; }

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
    /// 报表菜单
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_REPORT { get; set; }

    /// <summary>
    /// URL参数
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_PARAM { get; set; }

    /// <summary>
    /// URL变量
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_QUERY { get; set; }

    [ForeignKey(nameof(ID_MODULE))]
    public SYS_MODULE? SYS_MODULE { get; set; }
}
