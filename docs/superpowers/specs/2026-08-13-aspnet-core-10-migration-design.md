# ASP.NET Core 10 迁移设计

## 目标

使用面向本机单用户的 ASP.NET Core 10 应用替代现有 Python 应用，同时保留 CSV 导入、Oxigraph 持久化存储、SPARQL 查询、RDF 导出和 AntV G6 可视化能力。

应用通过 NuGet 使用 `Oxigraph.Extensions.DotNetRDF` `0.5.7`。该包以传递依赖方式提供兼容版本的 Oxigraph 和 dotNetRDF。核心存储和查询操作使用 Oxigraph 原生 API；dotNetRDF 互操作层用于图模型和序列化边界。

## 解决方案结构

创建包含以下四个项目的 `StockGraph.slnx`：

- `src/StockGraph.Core`：词汇表常量、RDF 术语构造、CSV 导入、存储协调、SPARQL 执行、RDF 导出和 G6 图投影。
- `src/StockGraph.Web`：ASP.NET Core 10 Minimal API、配置、OpenAPI、错误映射和静态 G6 应用。
- `tests/StockGraph.Core.Tests`：基于临时内存存储或文件存储的聚焦行为测试。
- `tests/StockGraph.Web.Tests`：使用 `WebApplicationFactory` 和隔离临时存储的端点集成测试。

CSV 解析使用 CsvHelper，以结构化解析器处理引号字段、编码、缺失值和灵活表头，不采用手写字符串处理。

## 组件

### StockGraph.Core

- `Vocabulary`：基础、RDF、RDFS、schema.org 和 XSD IRI。
- `RdfTermFactory`：生成与现有 Python 行为一致的稳定实体 IRI 和类型化字面量。
- `FinancialGraphBuilder`：分批加载行情、新闻和可选的 Neo4j 时代 CSV 输入，并返回构建统计信息。
- `OxigraphStoreCoordinator`：持有唯一打开的持久化存储，并协调查询、重建、导出、刷新和释放操作。
- `SparqlService`：使用已配置的前缀和默认图行为执行任意 SPARQL 及仓库内查询文件。
- `RdfExportService`：以流式方式导出 TriG、N-Quads、Turtle 或 N-Triples，并校验命名图参数。
- `GraphProjectionService`：构建有数量限制且去重的 G6 节点、边、元数据和统计信息。

### StockGraph.Web

- 按存储管理、SPARQL、导出和可视化数据分组的 Minimal API 端点。
- 面向源数据、存储、查询目录、图 IRI 和默认可视化限制的强类型配置。
- RFC 9457 Problem Details 响应和集中式异常映射。
- 位于 `/` 的静态 G6 工作区，包含紧凑工具栏、类型筛选器、全尺寸图画布和节点详情面板。

## API

### 健康检查

`GET /healthz` 报告应用和存储可用性，不暴露本机路径。

### 构建

`POST /api/store/build` 接受以下参数：

- `clear`
- `maxPriceRows`
- `maxNewsRows`
- `chunkSize`

响应包含插入的 quad 数、各输入文件的行数和跳过的可选文件。重建操作串行执行并持有存储操作独占锁，因此查询不会观察到只完成一部分的存储。

### SPARQL

`POST /api/sparql` 接受 `application/sparql-query`。SELECT 和 ASK 结果支持 SPARQL JSON 与 CSV；CONSTRUCT 和 DESCRIBE 结果支持 RDF 内容协商。请求取消会传递给 Oxigraph。

`GET /api/queries/{name}` 仅执行已配置查询目录中的 `.sparql` 文件，拒绝路径遍历和任意文件访问。

### 导出

`GET /api/export?format=trig|nq|ttl|nt` 以流式下载方式响应。Turtle 和 N-Triples 导出要求选择命名图；数据集格式导出完整数据集。

### 可视化

`GET /api/graph` 接受有边界限制的 `maxDaysPerStock`、`maxNews` 和 `maxRelationships` 参数，返回 G6 节点、边、生成时间、总数和按节点类型统计的数量。

`GET /` 提供可视化工作区。AntV G6 通过 CDN 加载固定版本的 ESM，避免引入 Node 构建链。页面包含明确的加载中、空图、依赖加载失败和 API 请求失败状态。

## 数据流与并发

应用启动时打开一个文件型 Oxigraph 存储，并在主机关闭时刷新和释放。协调器防止同一 RocksDB 目录存在多个打开的句柄。

普通查询支持取消。重建操作取得独占访问权，按需清空存储，以有界批次写入 quad，刷新后释放访问权。首要目标是本机单用户应用，因此优先保证可预测的存储一致性，而不是最大化并发吞吐量。

配置默认使用以下仓库相对路径：

- 源数据：`Financial-Knowledge-Graphs`
- 存储：`.oxigraph/financial_kg`
- 查询：`queries`
- 图：`https://stockgraph.local/kg/graph/main`

所有配置均可通过 `appsettings.json`、特定环境配置、环境变量或命令行配置覆盖。

## 错误处理

- 参数无效或格式不受支持时返回 `400` Problem Details。
- 存储管理操作发生冲突时返回 `409`。
- 请求导致的 SPARQL 解析或求值错误返回 `422`。
- 客户端取消且响应尚未开始时返回 `499`。
- 意外失败会记录日志并返回 `500`，不暴露本机路径、堆栈跟踪或原生库细节。

界面在工作区内呈现 API 错误，并允许用户重试或调整限制参数。

## 测试

实现过程按较小的行为切片遵循测试驱动开发。

核心测试覆盖：

- 股票、交易日、新闻、股东、概念、沪深股通、公告和相关性实体的确定性 IRI；
- 中文标签以及 RDF 类和谓词；
- 日期、日期时间、整数、小数和字符串字面量类型；
- 当前样例行情和新闻 CSV 的导入、限制、批处理、统计及可选文件报告；
- SPARQL SELECT、ASK、CONSTRUCT 和仓库查询文件执行；
- RDF 导出格式和命名图行为；
- G6 投影的数量限制、标签、元数据、样式分配、去重和统计。

Web 测试覆盖成功响应的内容类型、校验失败、Problem Details、查询文件白名单、导出下载、构建冲突，以及实际可测的取消行为。

端到端冒烟检查使用仓库内样例构建受限的临时存储，运行所有现有查询文件，加载可视化页面，并在桌面和移动视口验证非空的可交互图谱。

## 迁移与清理

迁移按行为切片推进：RDF 模型、样例导入、可选数据导入、查询、导出、图投影、API 托管和浏览器验证。在等价的 .NET 行为通过测试之前，保留现有 Python 文件。

证明行为等价后，删除 `pyproject.toml`、`requirements.txt`、`src` 下的 Python 包和四个 Python 脚本。保留全部数据、SPARQL 查询文件和研究文档。重写 README，说明 `dotnet restore`、`dotnet run`、`dotnet test` 工作流，构建、查询和导出 API 示例，可视化使用方式，配置覆盖方式，以及 Windows 下单进程存储锁限制。

## 验收标准

- 解决方案能够使用 .NET 10 SDK 和 `Oxigraph.Extensions.DotNetRDF` `0.5.7` 完成还原和构建。
- 受限构建能够把仓库内两份股票文件和新闻文件导入临时持久化存储。
- 现有 SPARQL 文件能够在迁移后的存储上成功执行。
- SPARQL 和导出响应使用内容协商所要求的正确内容类型。
- 内置 G6 页面能够在桌面和移动视口展示图数据并进行交互。
- 自动化测试通过，且不要求 Python 运行时。
- 行为等价验证完成后，不再保留 Python 应用和依赖清单。
