using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Modules.Ssamc.Data.Entities;

/// <summary>
/// 模块页面
/// </summary>
[Table("SYS_PAGE")]
public partial class SYS_PAGE
{
    /// <summary>
    /// ID
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
    /// 页面编码
    /// </summary>
    [StringLength(80)]
    [Unicode(false)]
    public string? STR_CODE { get; set; }

    /// <summary>
    /// 中文名称
    /// </summary>
    [StringLength(80)]
    [Unicode(false)]
    public string? STR_NAME { get; set; }

    /// <summary>
    /// 英文名称
    /// </summary>
    [StringLength(80)]
    [Unicode(false)]
    public string? STR_NAMEEN { get; set; }

    /// <summary>
    /// 页面类别
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? STR_TYPE { get; set; }

    /// <summary>
    /// 明细页面ID
    /// </summary>
    [Precision(19)]
    public long? ID_PAGE_DETAIL { get; set; }

    /// <summary>
    /// 页面模板
    /// </summary>
    [Precision(19)]
    public long? ID_TEMPLATE { get; set; }

    /// <summary>
    /// 查看权限
    /// </summary>
    [Precision(19)]
    public long? ID_SEE { get; set; }

    /// <summary>
    /// 主页
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_MAIN { get; set; }

    /// <summary>
    /// 启动页面
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_START { get; set; }

    /// <summary>
    /// 允许用户配置
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_USER_CONFIG { get; set; }

    /// <summary>
    /// 列表只有按钮
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_JUST_BUTTON { get; set; }

    /// <summary>
    /// 制单人查看控制
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_OWEN_SEE { get; set; }

    /// <summary>
    /// 字段进行权限控制
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_FIELD_RIGHT { get; set; }

    /// <summary>
    /// 禁用审批
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_FLOW_DIS { get; set; }

    /// <summary>
    /// 独立升级
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_ALONE { get; set; }

    /// <summary>
    /// 新增AJAX联动
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_NEW_AJAX { get; set; }

    /// <summary>
    /// 不合并工卡
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_DOUBLE { get; set; }

    /// <summary>
    /// 读库
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_READ { get; set; }

    /// <summary>
    /// 系统页面
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_SYS { get; set; }

    /// <summary>
    /// 窗口宽度
    /// </summary>
    [Precision(5)]
    public short? INT_WIN_WIDTH { get; set; }

    /// <summary>
    /// 窗口高度
    /// </summary>
    [Precision(5)]
    public short? INT_WIN_HEIGHT { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [Precision(5)]
    public short? INT_SORT { get; set; }

    /// <summary>
    /// 启用状态
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
    /// 定时刷新
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_REFRESH { get; set; }

    /// <summary>
    /// 刷新间隔
    /// </summary>
    [Precision(5)]
    public short? INT_REFRESH { get; set; }

    /// <summary>
    /// 默认导出模板
    /// </summary>
    [Precision(19)]
    public long? UP_KEY_PRINT { get; set; }

    /// <summary>
    /// 默认导出文件名
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_PRINT_NAME { get; set; }

    /// <summary>
    /// 默认导出API
    /// </summary>
    [StringLength(100)]
    [Unicode(false)]
    public string? STR_PRINT_API { get; set; }

    /// <summary>
    /// 菜单名称
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_MENU { get; set; }
}
