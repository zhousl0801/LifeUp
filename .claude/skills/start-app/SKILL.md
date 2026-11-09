---
name: start-app
description: 启动 LifeUp 本机 APP：默认连接 Supabase，启动后端 API（5069）和前端 Vite（5173），并验证服务与账本可读。用户要求启动项目或本地开发环境时使用。
---

# 启动 LifeUp 本机 APP

默认连接用户的 Supabase 真实数据库。本机 API 关闭自动记账和行情定时刷新，以免与云端重复执行。不要自动迁移、写入测试数据、触发业务操作或回显账本和凭据。用户明确指定其他数据库时，按其要求处理，不静默切换。

## 启动前检查

- 仓库根目录下的 `.local/start-supabase-api.sh` 从 macOS 钥匙串读取数据库密码，并使用 `.local/certificates/supabase-ca.crt` 验证服务器证书。两者都在 Git 忽略目录中；不要输出或提交凭据及证书。
- 检查脚本、证书是否可读，以及脚本中 `security find-generic-password -s` 指定的钥匙串项目是否可用；不要输出密码。如果任一缺失，说明缺少哪一项并请求用户协助，不回退到本地数据库，也不关闭证书校验。
- 先检查 5069、5173 端口。已运行的服务应先确认属于本项目；API 还需确认确实连接 Supabase 且本机两个定时任务已关闭，才能直接复用。仅凭健康接口或端口占用不能判断数据库。如果无法确认现有进程或端口被其他程序占用，不要擅自终止它；说明情况后再处理。
- 不需要启动 Colima 或本地 PostgreSQL 容器。

## 启动服务

从仓库根目录启动后端并保持进程运行：

```sh
./.local/start-supabase-api.sh
```

脚本通过环境变量设置连接及 `PlanScheduler__Enabled=false`、`QuoteRefreshScheduler__Enabled=false`，不会将密码写入文件。API 使用开发环境；不要改用会自动执行迁移的生产环境。成功启动时监听 `http://localhost:5069`，日志显示行情定时刷新未启用。

如前端未运行，在 `web` 目录执行 `npm run dev` 并保持进程运行；默认地址为 `http://localhost:5173`，`/api` 代理到后端。依赖缺失时按项目锁文件安装。

## 验证

仅输出状态码，勿输出个人财务数据：

```sh
curl -s -o /dev/null -w 'API 健康：%{http_code}\n' http://localhost:5069/api/health
curl -s -o /dev/null -w '代理健康：%{http_code}\n' http://localhost:5173/api/health
curl -s -o /dev/null -w '账本可读：%{http_code}\n' http://localhost:5173/api/ledger
```

三项均应为 200。健康接口不检查数据库；账本接口成功只能证明当前连接可读取，还需结合启动配置确认连接目标。如连接失败，检查证书、钥匙串和网络，保留 TLS 验证；不要自动切换数据库或重置密码。向用户报告访问地址、连接目标、检查结果和后台任务状态。
