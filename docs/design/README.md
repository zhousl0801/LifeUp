# UI 设计资料

视觉方向：**Midnight Mint v2.1（午夜薄荷金融风）**，支持 **Daylight Prism（亮色冷瓷白 + 晴空极光紫 Hero）** 与 **Obsidian Mint（暗色冷曜石黑 + 星夜靛紫 Hero）** 双主题，应用内提供「跟随系统 / 亮色 / 暗色」三态切换。本阶段聚焦手机端竖屏体验，电脑浏览器居中呈现手机画幅，独立桌面布局留待后续评估。

## 现行设计规范与原型

| 资料 | 状态 | 说明 |
| --- | --- | --- |
| [Midnight Mint 视觉规范](MidnightMint设计规范-2026-11-04.md) | 现行（v2.1） | 双主题色彩、5 色语义微彩、4+1 悬浮导航、1+2+2 双列流光资产卡、iOS Inset Grouped 表单与组件规则 |
| [CSS 色彩变量](ui-color-tokens.css) | 现行（v2.1） | 前端直接引入的唯一色彩 Token 源文件（亮色 Daylight Prism + 暗色 Obsidian Mint） |
| [UI 基础规范](UI基础规范-2026-10-28.md) | 现行（已同步） | 字号层级、间距圆角、触控目标、BottomSheet、PWA 安全区遮罩与异步 loading 约束 |
| [高保真交互原型说明](../../prototype/README.md) | 现行（v2.1） | 基于 Midnight Mint v2.1 的静态交互原型，覆盖五类资产、双列流光卡、账单预算日历、目标页与我的页 |
| [产品设计文档](../product/LifeUp-产品设计文档-V1.1.md) | 现行（V1.4） | 业务范围、页面结构、金额口径、状态流转与验收场景的唯一业务依据 |

## 功能专题设计规格

| 专题设计规格 | 状态 | 覆盖范围 |
| --- | --- | --- |
| [基金交易日历与持仓聚合](基金交易日历与持仓聚合-设计规格-2026-10-26.md) | 已实施 | 中/美/港三地交易日历判定、同基金 A/C 份额类别跨渠道聚合与定投排期规则 |
| [股票模块设计规格](股票模块-设计规格-2026-10-30.md) | 已实施 | A 股与场内 ETF / LOF 多持仓、买卖、行情刷新、今日盈亏、总资产纳入与历史重放口径 |
| [资本积累与目标设计规格](资本积累与目标-设计规格-2026-11-12.md) | 已实施 | 总资产目标、月度推演、玻璃轨道、自动里程碑、本月实际与备份口径 |
| [工资收入与年终奖设计规格](工资收入与年终奖-设计规格-2026-11-13.md) | 已实施 | 工资档案、工资/公积金/年终奖三计划同步、年终奖年度计划与资本预测联动 |

## 设计稿与参考截图

### 现行模块视觉参考

- [资本积累页 · 玻璃轨道](screenshots/capital-goal-glass-2026-11-12.png)
- [资本积累页 · 新增记录态参考](screenshots/capital-goal-glass-record-2026-11-12.png)

### 历史归档资料（仅供追溯，不作为现行视觉或结构依据）

| 归档资料 | 说明 |
| --- | --- |
| [UI 配色规范（v1.0 瓷白归档）](UI配色规范.md) | 早期瓷白 + 克莱因蓝配色历史稿，对应 Token 已从 `ui-color-tokens.css` 移除 |
| [早期整合线框](wireframes/index.html) | V1.0 评审用静态线框（含多参考混搭底色与四类资产旧结构），保留作设计演进追溯 |
| 早期 V1 手机截图 | [首页上半](screenshots/asset-home-v1-mobile.png)、[首页下半](screenshots/asset-home-v1-mobile-bottom.png)、[账本](screenshots/asset-ledger-v1-mobile.png)、[投资](screenshots/asset-invest-v1-mobile.png)（早期瓷白主题截图） |
| 早期页面线框图 | [首页](wireframes/LifeUp-首页线框稿.jpg)、[账本](wireframes/LifeUp-鲨鱼参考-账本.jpg)、[记一笔](wireframes/LifeUp-鲨鱼参考-记一笔.jpg)、[图表](wireframes/LifeUp-鲨鱼参考-图表.jpg)、[投资总览](wireframes/LifeUp-持仓参考-投资总览.jpg)、[基金明细](wireframes/LifeUp-持仓参考-基金明细.jpg)、[现金](wireframes/LifeUp-现金线框.jpg)、[公积金](wireframes/LifeUp-公积金线框.jpg)、[积存金](wireframes/LifeUp-积存金线框.jpg)、[计划](wireframes/LifeUp-自动计划线框.jpg)、[余额不足](wireframes/LifeUp-计划余额不足预览.jpg) |

## 使用约定

- **业务结构与口径**以 [产品设计文档](../product/LifeUp-产品设计文档-V1.1.md) 和对应功能专题设计规格为准。
- **现行视觉与交互**以 [Midnight Mint 视觉规范](MidnightMint设计规范-2026-11-04.md)、[CSS 色彩变量](ui-color-tokens.css) 及 [高保真交互原型](../../prototype/README.md) 为准。
- 本地查看交互原型请在仓库根目录启动静态服务并访问 `/prototype/`；不要从早期归档截图扩展房产、负债、多现金账户等产品边界外功能。
