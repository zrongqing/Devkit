using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ssamc.DB.Entities;

/// <summary>
/// 模块
/// </summary>
[Table("SYS_MODULE")]
public partial class SYS_MODULE
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
    /// 上级目录
    /// </summary>
    [Precision(19)]
    public long? ID_TOP { get; set; }

    /// <summary>
    /// 模块编码
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_CODE { get; set; }

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
    /// 模块类别
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? STR_TYPE { get; set; }

    /// <summary>
    /// 按钮图标
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_ICON { get; set; }

    /// <summary>
    /// 调用类别
    /// </summary>
    [StringLength(20)]
    [Unicode(false)]
    public string? STR_ACTION_TYPE { get; set; }

    /// <summary>
    /// 调用地址
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_ACTION { get; set; }

    /// <summary>
    /// 启用审批
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_FLOW { get; set; }

    /// <summary>
    /// 审批流程
    /// </summary>
    [Precision(19)]
    public long? ID_FLOW { get; set; }

    /// <summary>
    /// 自定义审批
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_FLOW_SELF { get; set; }

    /// <summary>
    /// 查看权限
    /// </summary>
    [Precision(19)]
    public long? ID_SEE { get; set; }

    /// <summary>
    /// 制单人查看控制
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_OWEN_SEE { get; set; }

    /// <summary>
    /// 制单人编辑控制
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_OWEN_EDIT { get; set; }

    /// <summary>
    /// 启用审批签名
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_SIGN_AUDIT { get; set; }

    /// <summary>
    /// 启用数据签名
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_SIGN_DATA { get; set; }

    /// <summary>
    /// 审批时可编辑数据
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_FLOW_EDIT { get; set; }

    /// <summary>
    /// 动态目录关键字
    /// </summary>
    [StringLength(32)]
    [Unicode(false)]
    public string? STR_DYNAMIC { get; set; }

    /// <summary>
    /// 配置模块
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_CONFIG { get; set; }

    /// <summary>
    /// 读库
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_READ { get; set; }

    /// <summary>
    /// 树形结构
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_TREE { get; set; }

    /// <summary>
    /// 事务控制
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_ATOM { get; set; }

    /// <summary>
    /// 是节点
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_NODE { get; set; }

    /// <summary>
    /// 有附件
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_ATT { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_NOTES { get; set; }

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
    /// 审批状态
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_AUDIT { get; set; }

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
    /// 数据库项目
    /// </summary>
    [Precision(19)]
    public long? ID_VER { get; set; }

    /// <summary>
    /// 数据库子项目
    /// </summary>
    [Precision(19)]
    public long? ID_VER_SUB { get; set; }

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
    /// 模块类别
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_MODE { get; set; }

    /// <summary>
    /// 禁用打印设置
    /// </summary>
    [Column(TypeName = "NUMBER(1)")]
    public bool? IS_PRINT { get; set; }

    /// <summary>
    /// 指定主机运行
    /// </summary>
    [Precision(19)]
    public long? ID_HOST { get; set; }

    /// <summary>
    /// 指定主机运行
    /// </summary>
    [StringLength(50)]
    [Unicode(false)]
    public string? STR_HOST { get; set; }
}
