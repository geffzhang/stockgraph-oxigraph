const API = '/api/graph';
const DEFAULT_PARAMS = { maxDaysPerStock: 30, maxNews: 50, maxRelationships: 200 };

const TYPE_COLORS = {
  Stock:       { fill: '#1890ff', stroke: '#096dd9' },
  TradingDay:  { fill: '#52c41a', stroke: '#389e0d' },
  NewsArticle: { fill: '#faad14', stroke: '#d48806' },
};

let graph = null;

async function loadGraph(params = DEFAULT_PARAMS) {
  showLoading(true);
  hideError();
  try {
    const qs = new URLSearchParams(params).toString();
    const res = await fetch(`${API}?${qs}`);
    if (!res.ok) throw new Error(`API ${res.status}`);
    const data = await res.json();
    renderGraph(data);
    updateStats(data.metadata);
  } catch (e) {
    showError(`加载失败: ${e.message}`);
  } finally {
    showLoading(false);
  }
}

function renderGraph(data) {
  if (graph) { graph.destroy(); graph = null; }

  const container = document.getElementById('graph-container');
  graph = new G6.Graph({
    container,
    width: container.offsetWidth,
    height: container.offsetHeight,
    modes: { default: ['drag-canvas', 'zoom-canvas', 'drag-node'] },
    defaultNode: {
      type: 'circle',
      size: 24,
      style: { fill: '#e8f4fd', stroke: '#1890ff', lineWidth: 1.5, cursor: 'pointer' },
      labelCfg: { style: { fontSize: 11 } },
    },
    defaultEdge: { type: 'line', style: { stroke: '#ccc', lineWidth: 1 } },
    nodeStateStyles: { hidden: { opacity: 0 } },
    edgeStateStyles: { hidden: { opacity: 0 } },
    layout: { type: 'force', preventOverlap: true, nodeSize: 30 },
  });

  const nodes = data.nodes.map(n => ({
    id: n.id,
    label: n.label,
    type: n.type,
    size: n.type === 'Stock' ? 32 : n.type === 'TradingDay' ? 16 : 20,
    style: TYPE_COLORS[n.type] || {},
  }));

  const edges = data.edges.map(e => ({
    source: e.source,
    target: e.target,
    label: e.type,
  }));

  graph.data({ nodes, edges });
  graph.render();

  graph.on('node:click', (evt) => {
    const node = evt.item;
    const model = node.getModel();
    showDetail(model);
  });

  graph.on('canvas:click', () => hideDetail());
}

function showDetail(node) {
  const panel = document.getElementById('detail-panel');
  document.getElementById('detail-title').textContent = `${node.type}: ${node.label}`;
  const dl = document.getElementById('detail-content');
  dl.innerHTML = `<dt>ID</dt><dd>${node.id}</dd><dt>类型</dt><dd>${node.type}</dd>`;
  panel.classList.remove('hidden');
}

function hideDetail() {
  document.getElementById('detail-panel').classList.add('hidden');
}

function updateStats(meta) {
  const counts = Object.entries(meta.nodeTypeCounts)
    .map(([k, v]) => `${k}: ${v}`)
    .join(' | ');
  document.getElementById('stats').textContent = `节点: ${meta.totalNodes} | 边: ${meta.totalEdges} | ${counts}`;
}

function showLoading(v) {
  document.getElementById('loading').classList.toggle('hidden', !v);
}

function showError(msg) {
  const el = document.getElementById('error');
  el.textContent = msg;
  el.classList.remove('hidden');
}

function hideError() {
  document.getElementById('error').classList.add('hidden');
}

// Init
document.getElementById('btn-reload').addEventListener('click', () => loadGraph());
document.getElementById('btn-close-panel').addEventListener('click', hideDetail);

document.getElementById('filter-type').addEventListener('change', (e) => {
  const type = e.target.value;
  if (!graph) return;

  graph.getNodes().forEach(n => {
    graph.setItemState(n, 'hidden', type && n.getModel().type !== type);
  });
  graph.getEdges().forEach(ed => {
    const srcType = ed.getSource().getModel().type;
    const tgtType = ed.getTarget().getModel().type;
    graph.setItemState(ed, 'hidden', type && (srcType !== type || tgtType !== type));
  });
});

window.addEventListener('resize', () => {
  if (graph) {
    const c = document.getElementById('graph-container');
    graph.changeSize(c.offsetWidth, c.offsetHeight);
  }
});

loadGraph();
