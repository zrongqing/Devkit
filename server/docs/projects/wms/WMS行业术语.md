# 制造业 WMS 行业术语表

现场评审、作业页面和接口说明共用这份词表。“本项目”指[系统需求文档](wms.md)中的首版建议口径。目录涵盖后续候选能力，交付范围以需求文档中的 P0、P1 为准。

## 仓库、物料与标识

| 中文名称 | 英文／缩写 | 现场含义与例子 |
| --- | --- | --- |
| 仓储管理系统 | Warehouse Management System · WMS | 指导并记录仓库实际收、发、存、移、盘。可按原料批号查询当前所在库位以及发给过哪些工单。 |
| 企业资源计划系统 | Enterprise Resource Planning · ERP | 管采购、计划、财务等企业业务。采购订单给出订购量，WMS 回传实际收货量，形成对账依据。 |
| 制造执行系统 | Manufacturing Execution System · MES | 记录车间执行、投料和产出。本项目由 MES 提供实际耗用及报工依据。 |
| 质量管理系统 | Quality Management System · QMS | 管理检验与处置结论。WMS 根据合格、待判或不合格的数量控制库存用途。 |
| 仓库／库区／库位 | Warehouse / Zone / Bin Location | 仓库是管理范围，库区按用途划分，库位是可以定位实物的位置。例如原料仓 → 收货区 → R-01。 |
| 线边仓 | Line-side Store | 产线附近暂存已领材料的仓储位置。本项目按工单登记交接、耗用和退回。 |
| 原料、半成品、成品 | Raw Material / Semi-finished Good / Finished Good | 同一零件在上游工单可作为产出入库，在下游工单又可作为组件领用。 |
| 在制品 | Work in Process · WIP | 已进入加工过程、尚未完工的产品。送到线边等待投料的原料，在本项目仍有独立的线边原料余额。 |
| 物料编码／库存保有单位 | Item Code / Stock Keeping Unit · SKU | 物料编码标识主数据，SKU 表示独立管理库存的品种或规格。不同规格的轴承通常分别统计，批次用于区分同一品种的不同来料。 |
| 计量单位／基本单位 | Unit of Measure · UOM / Base UOM | 基本单位用于统一记账，作业单位可以是箱或托。例如 1 箱 12 件，收 2 箱等于入账 24 件。 |
| 批次／批号 | Lot, Batch / Lot Number | 按共同来源或生产条件划分的一组实物及其标识。追溯时同时带物料与批号；内部批号需能回查供应商批号。 |
| 序列号 | Serial Number · SN | 标识单个实物。20 个同批次电机可共用一个批号，但逐台管理时各自有序列号。 |
| 物流单元／容器号 | Logistic Unit / License Plate Number · LPN | 箱、托等可作为一个整体搬运。LPN 是系统给该容器或搬运单元的识别号，扫描后可查装载明细。 |
| 全球贸易项目代码 | Global Trade Item Number · GTIN | GS1 对贸易项目的标识。单件与整箱作为不同贸易项目时可有各自的 GTIN，扫描后须映射到物料与包装单位。 |
| 系列货运包装箱代码 | Serial Shipping Container Code · SSCC | GS1 用 18 位数字标识一个物流单元，例如一托混装货物。企业内部 LPN 可关联 SSCC。 |
| 应用标识符 | Application Identifier · AI | GS1 标签上的字段含义，例如 (00) 为 SSCC，(01) 为 GTIN，(10) 为批号，(17) 为有效期，(21) 为序列号。括号用于人眼识读。 |

例如，一托装有两批轴承。托盘 SSCC 能定位该托，箱上 GTIN 能识别箱装规格，批号能区分两批来料。库存明细仍需分别记录两批的数量。[GS1 物流标签指南，第 4 章](https://ref.gs1.org/guidelines/logistic-label/) · [GS1 应用标识符目录](https://ref.gs1.org/ai/)

## 到货、检验与库存

| 中文名称 | 英文／缩写 | 现场含义与例子 |
| --- | --- | --- |
| 采购订单 | Purchase Order · PO | 约定采购什么、订多少。WMS 将分次收货实绩归集到相应的订单行。 |
| 预先发货通知 | Advance Shipping Notice · ASN | 供应方提前告知预计到货内容。ASN 上有 100 件，现场可先实收 80 件。 |
| 收货／上架 | Receiving / Putaway | 收货确认货到了多少，上架确认从收货位搬到哪个存储位。本项目在收货确认时增加现存。 |
| 来料检验／质量放行 | Incoming Quality Control · IQC / Quality Release | 对来料作检验，由授权人员判定全部或部分可用。一个批次可以有部分放行、部分隔离。 |
| 隔离／冻结 | Quarantine / Hold | 隔离让待判或异常品与正常品分开管理；冻结限制指定库存的使用。冻结可以先执行，实物随后移入隔离位。 |
| 现存量 | On-hand Quantity | 某一时点仓内实物账面数量，包含待检、隔离及已预留部分。按库位汇总可得仓库总量。 |
| 预留量／分配 | Reserved Quantity / Allocation | 预留是为需求保留的数量，分配是选定供料库存的动作。本项目按批次和库位分配并形成预留。 |
| 可用量 | Available Quantity | 当前可给新任务分配的数量。若可拣位有合格未过期料 80 件，其中预留 25 件，则可用 55 件。 |
| 先进先出 | First In, First Out · FIFO | 对有资格发出的库存优先选择较早收货者。这里用于实物拣料顺序，成本核算由 ERP 负责。 |
| 先到期先出 | First Expired, First Out · FEFO | 本项目在可发批次中优先选择有效期较早者，即使它更晚到货。过期、隔离批次先从候选中剔除。 |
| 移库／调拨 | Bin Transfer / Stock Transfer | 移库常指同仓换位；调拨可涉及不同仓库。即时交接可同步记两端，分开发运与接收时还需管理在途量。 |
| 库存流水／过账／冲销 | Stock Ledger Entry / Posting / Reversal | 过账把已确认的动作计入台账；流水说明由哪张单、在哪个位置增减了多少。冲销用反向记录更正原实绩。 |

部分收货与收货后上架的实际操作可参见 OpenBoxes，按效期或入库时间选择库存可参见 Odoo 的出库策略。[OpenBoxes 收货作业](https://help.openboxes.com/article/48-receiving) · [Odoo 出库策略](https://www.odoo.com/documentation/14.0/applications/inventory_and_mrp/inventory/routes/strategies/removal.html)

## 工单备料、生产领退料与成品

| 中文名称 | 英文／缩写 | 现场含义与例子 |
| --- | --- | --- |
| 生产工单 | Work Order · WO / Manufacturing Order · MO | 一次生产任务，说明产什么、产多少、由哪条线执行。WMS 依据已下达的用料行备料。 |
| 物料清单 | Bill of Materials · BOM | 定义某版本产品的组件及标准用量。一个成品标准用两只轴承，工单计划做 26 个则理论需 52 只；实耗以现场记录为准。 |
| 齐套／缺料 | Material Kit Readiness / Material Shortage | 指工单截至需料时点各用料行是否均备足。数量多的螺钉不能抵消轴承的缺口。 |
| 备料／拣料 | Material Staging / Picking | 备料是为生产准备物料的整项作业，拣料是从指定库位取出指定物料与批次，可移至备料位等待交接。 |
| 生产领料／交接 | Material Issue to Production / Handover | 仓库把物料发给指定工单，车间确认实际收到的批次和数量。本项目在签收时把保管位置改为线边。 |
| 实际耗用／投料 | Actual Material Consumption / Material Input | 生产使用物料的事实，记录工单、投入批次、实际数量。例如领 60、实际投 52，线边还应能找到 8。 |
| 生产退料 | Production Material Return | 将工单未耗余料交回仓库，关联原领料行。质量待判的退料先隔离或待检，按检验结论恢复可用。 |
| 生产补料／仓内补货 | Additional Material Issue / Replenishment | 补料给某张工单追加供料，例如替换损坏件；仓内补货把储存位物料搬到拣选位，目的是让拣选位有货。 |
| 超领／替代料 | Excess Issue / Substitute Material | 超领是累计净领量超过批准需求，替代料是经批准用另一物料满足需求；二者都要追踪审批及工单用料行。 |
| 倒冲 | Backflushing | 按报工产量和标准用量反算物料消耗。本评审稿采用实际耗用回传，倒冲属于需要现场另行决定的策略。 |
| 报工／成品入库 | Production Reporting / Finished Goods Receipt | 报工说明产线完成了什么及产出数量，入库说明仓库实际接收了哪一批、多少件。例如报工 26，可先入 20，再入 6。 |
| 批次谱系／正反向追溯 | Lot Genealogy / Forward and Backward Traceability | 正向从原料找受影响的产出，反向从成品找投入。同一工单有多个产出批次时，只有明确的投入产出记录才能逐批对应。 |

ERPNext 对调料到生产仓、退回未耗物料和分批完工有具体示例；这些单据对应的动作可用于对照工厂现有表单。[ERPNext 工单指南](https://docs.frappe.io/erpnext/work-order) · [ERPNext 实际耗用](https://docs.frappe.io/erpnext/material_consumption)

## 盘点与后续作业

| 中文名称 | 英文／缩写 | 现场含义与例子 |
| --- | --- | --- |
| 循环盘点 | Cycle Counting | 分次选择库位或物料核查，逐步覆盖库存。可提高高价值或高频领用物料的盘点频率。 |
| 盲盘／复盘 | Blind Count / Recount | 盲盘时盘点员看不到账面数；出现差异时重新清点，保留两次结果、人员和时间。 |
| 盘盈／盘亏／库存调整 | Inventory Gain / Inventory Loss / Stock Adjustment | 实盘比账面多为盘盈，少为盘亏。审批后按差额调整，例如账面 30、实盘 29，调整 −1。 |
| 波次 | Wave | 把多个需求按时段、路线或产线编为一组，集中释放拣选任务。可用于同时给多个班组备料。 |
| 复核／装箱／发运 | Verification / Packing / Shipping | 发货前核对实物，记录货物与箱托的装载关系，交承运方后确认实际发运。适用于后续成品销售出库。 |

盘点差异处理及波次释放流程可对照行业产品的作业说明。[Microsoft 循环盘点](https://learn.microsoft.com/zh-cn/dynamics365/supply-chain/warehousing/cycle-counting) · [Microsoft 波次处理](https://learn.microsoft.com/zh-cn/dynamics365/supply-chain/warehousing/wave-processing)
