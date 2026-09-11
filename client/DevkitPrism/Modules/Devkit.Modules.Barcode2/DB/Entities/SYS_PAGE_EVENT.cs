using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Barcode2.DB.Entities;

/// <summary>
/// 页面事件
/// </summary>
[Table("SYS_PAGE_EVENT")]
public class SYS_PAGE_EVENT
{
    /// <summary>
    /// ID
    /// </summary>
    [Key]
    public long ID { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_NAME { get; set; }

    /// <summary>
    /// 事件归属
    /// </summary>
    [StringLength(20)]
    public string? STR_CLASS { get; set; }

    /// <summary>
    /// 事件类别
    /// </summary>
    [StringLength(30)]
    public string? STR_TYPE { get; set; }

    /// <summary>
    /// 执行类别
    /// </summary>
    [StringLength(30)]
    public string? STR_EXEC_TYPE { get; set; }

    /// <summary>
    /// 执行代码
    /// </summary>
    [StringLength(50)]
    public string? STR_EXEC_CODE { get; set; }

    /// <summary>
    /// 视图字段
    /// </summary>
    // [Precision(19)]
    public long? ID_VIEW_FIELD { get; set; }

    /// <summary>
    /// 数据集为空
    /// </summary>

    public int? IS_NULL { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    // [Precision(5)]
    public int? INT_SORT { get; set; }

    /// <summary>
    /// 单据状态
    /// </summary>

    public int? IS_STATE { get; set; }

    /// <summary>
    /// 删除标记
    /// </summary>

    public int? IS_DELETE { get; set; }

    /// <summary>
    /// 视图
    /// </summary>
    // [Precision(19)]
    public long? ID_VIEW { get; set; }

    /// <summary>
    /// 窗口宽度
    /// </summary>
    public int? INT_WIN_WIDTH { get; set; }

    /// <summary>
    /// 窗口高度
    /// </summary>
    public int? INT_WIN_HEIGHT { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    [StringLength(50)]
    public string? STR_CODE { get; set; }

    /// <summary>
    /// 有修改
    /// </summary>
    public int? IS_MODIFY { get; set; }

    /// <summary>
    /// 事件来源
    /// </summary>
    // [StringLength(20)]
    public string? STR_FROM { get; set; }

    /// <summary>
    /// AJAX取值
    /// </summary>
    public int? IS_AJAX { get; set; }

    /// <summary>
    /// 处理标志
    /// </summary>
    public int? IS_DO { get; set; }

    /// <summary>
    /// 操作日期
    /// </summary>
    // [StringLength(20)]
    // [Unicode(false)]
    public string? DT_UP { get; set; }

    /// <summary>
    /// 禁止联动
    /// </summary>

    public int? IS_LINKAGE { get; set; }

    /// <summary>
    /// 登录认证
    /// </summary>

    public int? IS_LOGIN { get; set; }

    /// <summary>
    /// 打包编译
    /// </summary>

    public int? IS_PACK { get; set; }

    /// <summary>
    /// 返回操作类别
    /// </summary>
    // [StringLength(20)]
    public string? STR_RETURN_TYPE { get; set; }

    /// <summary>
    /// 本地调试
    /// </summary>

    public int? IS_DEBUG { get; set; }

    /// <summary>
    /// 开发项目
    /// </summary>
    // [Precision(19)]
    public long? ID_PROJECT { get; set; }

    /// <summary>
    /// HTML
    /// </summary>

    public int? IS_HTML { get; set; }

    /// <summary>
    /// 任务节点
    /// </summary>
    // [Precision(19)]
    public long? ID_CENTER_NODE { get; set; }

    /// <summary>
    /// 返回不做处理
    /// </summary>

    public int? IS_ORIGINAL { get; set; }

    /// <summary>
    /// 升级状态
    /// </summary>

    public int? IS_UPGRADE { get; set; }

    /// <summary>
    /// 目标日期
    /// </summary>
    // [StringLength(20)]
    // [Unicode(false)]
    public string? DT_UPGRADE { get; set; }

    /// <summary>
    /// 删除日期
    /// </summary>
    // [StringLength(20)]
    // [Unicode(false)]
    public string? DT_DELETE { get; set; }

    /// <summary>
    /// 载入时有效
    /// </summary>

    public int? IS_LOAD { get; set; }

    /// <summary>
    /// 指定服务器运行
    /// </summary>
    // [Precision(19)]
    public long? ID_HOST { get; set; }

    /// <summary>
    /// 数据库
    /// </summary>
    // [Precision(19)]
    public long? ID_DB { get; set; }

    /// <summary>
    /// 数据库
    /// </summary>
    // [StringLength(20)]
    // [Unicode(false)]
    public string? STR_DB { get; set; }

    /// <summary>
    /// 此列表的字段进行操作
    /// </summary>
    // [Precision(19)]
    public long? ID_PAGE_LIST_TO { get; set; }

    /// <summary>
    /// 对此列表进行操作
    /// </summary>
    // [Precision(19)]
    public long? ID_PAGE_LIST_FROM { get; set; }

    /// <summary>
    /// 列表名称
    /// </summary>
    // [Precision(19)]
    public long? ID_PAGE_LIST { get; set; }

    /// <summary>
    /// 页面名称
    /// </summary>
    // [Precision(19)]
    public long? ID_PAGE { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    // [Precision(19)]
    public long? ID_MODULE { get; set; }

    /// <summary>
    /// 归属名称
    /// </summary>
    // [Precision(19)]
    public long? ID_MAIN { get; set; }

    /// <summary>
    /// 源代码
    /// </summary>
    // [Precision(19)]
    public long? ID_CODE { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    // [Precision(19)]
    public long? ID_BY { get; set; }

    /// <summary>
    /// 定服务器运行
    /// </summary>
    // [StringLength(50)]
    // [Unicode(false)]
    public string? STR_HOST { get; set; }

    /// <summary>
    /// 选择框返回SQL
    /// </summary>
    // [StringLength(500)]
    // [Unicode(false)]
    public string? STR_RETURN_SQL { get; set; }
}
