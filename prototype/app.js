/* LifeUp · Midnight Mint v2.1 高保真交互原型脚本（虚构数据，刷新重置） */
(() => {
  const state = {
    page: 'home',
    prevPage: 'home',
    themePref: 'light', // 'system' | 'light' | 'dark'
    amountsMasked: false, // 原型默认展开方便浏览，可点击 Hero 眼睛一键切换脱敏
    changeScope: 'day', // 'day' | 'month'
    trendRange: 'week', // 'week' | 'month' | 'quarter' | 'all'
    trendIdx: null,
    ledgerView: 'calendar', // 'calendar' | 'list' | 'chart'
    ledgerChartKind: 'expense',
    ledgerChartLevel: 'sub', // 'top' | 'sub'
    selectedCalDay: 19,
    monthlyBudget: 8000,
    fundGroup: '全部',
    fundView: 'holdings', // 'holdings' | 'returns' | 'trades' | 'plans'
    stockSort: 'value', // 'value' | 'profit' | 'name'
    salaryMasked: true,
    useSalaryProfile: true,
    capitalGoalWan: 100,
    capitalAnnualReturn: 7.5,
    entry: { kind: 'expense', category: '餐饮', group: '必要', tone: 'amber', expr: '0', note: '' },
    salary: {
      basePreTax: 18000,
      netMonthly: 12000,
      providentMonthly: 5200,
      bonusMonths: 2,
      bonusNet: 30000,
      bonusMonth: 1,
      payDay: 15
    },
    assets: {
      cash: 23400,
      fundConfirmed: 167700,
      fundPending: 300,
      stock: 51500,
      provident: 68000,
      goldGrams: 30,
      goldPrice: 900,
      goldCost: 25600,
      goldRealized: 412.5
    },
    monthFlow: { income: 12000, expense: 6800 },
    entries: [
      { id: 1, day: 19, date: '2026-11-19', kind: 'expense', group: '必要', cat: '餐饮', tone: 'amber', amount: 42.5, note: '工作餐与咖啡' },
      { id: 2, day: 19, date: '2026-11-19', kind: 'expense', group: '必要', cat: '交通', tone: 'sky', amount: 14, note: '地铁通勤' },
      { id: 3, day: 18, date: '2026-11-18', kind: 'expense', group: '弹性', cat: '购物', tone: 'rose', amount: 399, note: '冬季保暖外套' },
      { id: 4, day: 15, date: '2026-11-15', kind: 'income', group: '经常收入', cat: '工资', tone: 'emerald', amount: 12000, note: '自动记账 · 工资档案', auto: true },
      { id: 5, day: 12, date: '2026-11-12', kind: 'expense', group: '固定', cat: '居家', tone: 'amber', amount: 3200, note: '房租与水电网' },
      { id: 6, day: 8, date: '2026-11-08', kind: 'expense', group: '弹性', cat: '娱乐', tone: 'rose', amount: 268, note: '周末电影与聚餐' },
      { id: 7, day: 3, date: '2026-11-03', kind: 'expense', group: '必要', cat: '餐饮', tone: 'amber', amount: 2876.5, note: '本月日常餐饮汇总' }
    ],
    cashMoves: [
      { date: '2026-11-19', type: 'flow', title: '基金定投扣款 · 纳指100 C', amount: -300, note: '现金 → 基金买入待确认' },
      { date: '2026-11-15', type: 'flow', title: '公积金提取到账', amount: 2000, note: '公积金 → 统一现金池' },
      { date: '2026-11-15', type: 'income', title: '工资到账（自动记账）', amount: 12000, note: '工资档案托管' }
    ],
    funds: [
      {
        name: '摩根纳斯达克100指数(QDII)',
        group: '纳斯达克',
        assetValue: 86300,
        pending: 300,
        cost: 74200,
        dayProfit: 486.2,
        dayPct: 0.57,
        realized: 1240,
        classes: [
          { code: '019172', cls: 'A类', shares: 31200.45, nav: 1.6820, navDate: '11-18', value: 52479.16 },
          { code: '019173', cls: 'C类', shares: 20180.12, nav: 1.6611, navDate: '11-18', value: 33520.84 }
        ]
      },
      {
        name: '博时标普500ETF联接(QDII)',
        group: '标普500',
        assetValue: 53200,
        pending: 0,
        cost: 46800,
        dayProfit: 192.4,
        dayPct: 0.36,
        realized: 680,
        classes: [
          { code: '050025', cls: 'A类', shares: 18420.5, nav: 2.8881, navDate: '11-18', value: 53200 }
        ]
      },
      {
        name: '华泰柏瑞红利低波ETF联接',
        group: '红利',
        assetValue: 28500,
        pending: 0,
        cost: 27100,
        dayProfit: -64.8,
        dayPct: -0.23,
        realized: 320,
        classes: [
          { code: '007467', cls: 'C类', shares: 19256.75, nav: 1.4800, navDate: '11-19', value: 28500 }
        ]
      }
    ],
    stocks: [
      { market: '深市', code: '159915', name: '创业板ETF', shares: 8000, price: 2.435, avgCost: 2.210, dayProfit: 184.0, totalProfit: 1800.0 },
      { market: '沪市', code: '510300', name: '沪深300ETF', shares: 5000, price: 4.124, avgCost: 3.910, dayProfit: 95.0, totalProfit: 1070.0 },
      { market: '深市', code: '300750', name: '宁德时代', shares: 50, price: 228.000, avgCost: 210.400, dayProfit: -115.0, totalProfit: 880.0 }
    ],
    plans: [
      { id: 'p1', group: '基金定投', name: '摩根纳斯达克100 C', amount: 300, rule: '三地交易日 14:30', managed: false, paused: false },
      { id: 'p2', group: '基金定投', name: '博时标普500 A', amount: 300, rule: '每周三 14:30', managed: false, paused: false },
      { id: 'p3', group: '公积金', name: '公积金每月缴存', amount: 5200, rule: '每月 15 日 09:00', managed: true, paused: false },
      { id: 'p4', group: '公积金', name: '公积金每月提取', amount: 2000, rule: '每月 15 日 10:00', managed: false, paused: false },
      { id: 'p5', group: '工资收入', name: '每月税后工资', amount: 12000, rule: '每月 15 日 08:30', managed: true, paused: false },
      { id: 'p6', group: '工资收入', name: '年终奖到账', amount: 30000, rule: '每年 1 月 15 日', managed: true, paused: false }
    ]
  };

  const categoryGroups = {
    expense: [
      { name: '固定', items: [{ cat: '居家', tone: 'amber', icon: '居' }, { cat: '通讯', tone: 'sky', icon: '讯' }] },
      { name: '必要', items: [{ cat: '餐饮', tone: 'amber', icon: '餐' }, { cat: '交通', tone: 'sky', icon: '行' }, { cat: '日用', tone: 'emerald', icon: '用' }, { cat: '医疗', tone: 'rose', icon: '医' }] },
      { name: '弹性', items: [{ cat: '购物', tone: 'rose', icon: '购' }, { cat: '娱乐', tone: 'rose', icon: '乐' }, { cat: '学习', tone: 'violet', icon: '学' }, { cat: '人情', tone: 'violet', icon: '礼' }] }
    ],
    income: [
      { name: '经常收入', items: [{ cat: '工资', tone: 'emerald', icon: '薪' }, { cat: '奖金', tone: 'emerald', icon: '奖' }] },
      { name: '其他收入', items: [{ cat: '报销', tone: 'sky', icon: '报' }, { cat: '其他', tone: 'violet', icon: '余' }] }
    ]
  };

  const trendSeries = {
    week: [
      ['11-13', 332800], ['11-14', 333400], ['11-15', 335100], ['11-16', 335800],
      ['11-17', 336200], ['11-18', 337080], ['11-19', 337900]
    ],
    month: [
      ['10-21', 324600], ['10-26', 326400], ['11-01', 329100], ['11-06', 331200],
      ['11-11', 333800], ['11-15', 335100], ['11-19', 337900]
    ],
    quarter: [
      ['08-20', 301200], ['09-05', 307800], ['09-20', 313400], ['10-05', 318900],
      ['10-20', 324600], ['11-05', 331000], ['11-19', 337900]
    ],
    all: [
      ['04-01', 265000], ['06-01', 281400], ['08-01', 298200], ['09-15', 311800],
      ['10-20', 324600], ['11-19', 337900]
    ]
  };

  const fmt = (n, dec = 2) => Number(n).toLocaleString('zh-CN', { minimumFractionDigits: dec, maximumFractionDigits: dec });
  const mask = (text, force = false) => (state.amountsMasked || force) ? '••••' : text;
  const signed = (n) => (n > 0 ? '+' : '') + fmt(n);
  const goldVal = () => state.assets.goldGrams * state.assets.goldPrice;
  const fundTotal = () => state.assets.fundConfirmed + state.assets.fundPending;
  const totalAssets = () => state.assets.cash + fundTotal() + state.assets.stock + state.assets.provident + goldVal();

  function eyeSvg(masked) {
    return masked
      ? '<svg viewBox="0 0 24 24"><path d="M3 3l18 18"/><path d="M10.6 10.6a2 2 0 0 0 2.8 2.8"/><path d="M9.9 5.1A10.5 10.5 0 0 1 12 5c5 0 9 4.5 10 7a11.4 11.4 0 0 1-3.3 4.3"/><path d="M6.6 6.6A11.3 11.3 0 0 0 2 12c1 2.5 5 7 10 7 1.4 0 2.7-.3 3.9-.8"/></svg>'
      : '<svg viewBox="0 0 24 24"><path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/></svg>';
  }

  let toastTimer;
  function toast(msg) {
    const el = document.getElementById('toast');
    el.textContent = msg;
    el.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { el.hidden = true; }, 2400);
  }

  function applyTheme(pref) {
    state.themePref = pref;
    const isDark = pref === 'dark' || (pref === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.dataset.colorScheme = isDark ? 'dark' : 'light';
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.setAttribute('content', isDark ? '#0B0C10' : '#F4F6FA');
    document.querySelectorAll('[data-theme-pref]').forEach(b => b.classList.toggle('active', b.dataset.themePref === pref));
  }

  function openSheet(title, html) {
    document.getElementById('sheet-title').textContent = title;
    document.getElementById('sheet-body').innerHTML = html;
    document.getElementById('sheet-backdrop').hidden = false;
  }
  function closeSheet() {
    document.getElementById('sheet-backdrop').hidden = true;
  }

  function go(page) {
    closeSheet();
    if (page !== state.page) state.prevPage = state.page;
    state.page = page;
    document.getElementById('phone').dataset.page = page;
    document.querySelectorAll('.screen').forEach(s => s.classList.toggle('active', s.id === `screen-${page}`));
    document.querySelectorAll('#app-nav [data-nav]').forEach(b => {
      const target = b.dataset.nav;
      const active = target === page
        || (target === 'home' && ['cash', 'funds', 'stocks', 'provident', 'gold'].includes(page))
        || (target === 'ledger' && page === 'categories')
        || (target === 'capital' && page === 'plans')
        || (target === 'mine' && ['salary', 'backup'].includes(page));
      b.classList.toggle('active', active);
    });
    document.getElementById('app-content').scrollTop = 0;
    render();
  }

  function render() {
    const map = {
      home: renderHome,
      ledger: renderLedger,
      categories: renderCategories,
      entry: renderEntry,
      capital: renderCapital,
      plans: renderPlans,
      mine: renderMine,
      salary: renderSalary,
      backup: renderBackup,
      cash: renderCash,
      funds: renderFunds,
      stocks: renderStocks,
      provident: renderProvident,
      gold: renderGold
    };
    map[state.page]?.();
  }

  function buildTrendSvg(points) {
    const w = 340, h = 104, padX = 8, padY = 12;
    const vals = points.map(p => p[1]);
    const min = Math.min(...vals), max = Math.max(...vals);
    const span = Math.max(max - min, 1);
    const coords = points.map((p, i) => [
      padX + (i / Math.max(points.length - 1, 1)) * (w - padX * 2),
      h - padY - ((p[1] - min) / span) * (h - padY * 2)
    ]);
    const lineD = coords.map((c, i) => `${i === 0 ? 'M' : 'L'}${c[0].toFixed(1)},${c[1].toFixed(1)}`).join(' ');
    const areaD = `${lineD} L${coords[coords.length - 1][0].toFixed(1)},${h} L${coords[0][0].toFixed(1)},${h} Z`;
    const activeI = state.trendIdx == null ? coords.length - 1 : Math.min(state.trendIdx, coords.length - 1);
    const [ax, ay] = coords[activeI];
    return {
      date: points[activeI][0],
      val: points[activeI][1],
      svg: `<svg class="trend-svg" id="home-trend-svg" viewBox="0 0 ${w} ${h}">
        <defs>
          <linearGradient id="mintArea" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#7CE7B1" stop-opacity="0.34"/>
            <stop offset="100%" stop-color="#7CE7B1" stop-opacity="0"/>
          </linearGradient>
        </defs>
        <path d="${areaD}" fill="url(#mintArea)"/>
        <path d="${lineD}" fill="none" stroke="var(--color-hero-line)" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>
        <circle cx="${ax.toFixed(1)}" cy="${ay.toFixed(1)}" r="4.5" fill="#FFFFFF" stroke="var(--color-hero-line)" stroke-width="2.5"/>
      </svg>`
    };
  }

  function renderHome() {
    const total = totalAssets();
    const parts = fmt(total).split('.');
    const pts = trendSeries[state.trendRange].map((p, i, arr) => i === arr.length - 1 ? [p[0], total] : p);
    const tr = buildTrendSvg(pts);
    const dayChange = 721.3;
    const monthChange = 13300 + (total - 337900);
    const changeVal = state.changeScope === 'day' ? dayChange : monthChange;
    const changePct = state.changeScope === 'day' ? '+0.21%' : '+4.10%';

    const inc = state.monthFlow.income;
    const exp = state.monthFlow.expense;
    const flowSum = Math.max(inc + exp, 1);
    let incW = (inc / flowSum) * 100;
    let expW = (exp / flowSum) * 100;
    if (state.amountsMasked) { incW = 50; expW = 50; }

    const cards = [
      { key: 'cash', name: '现金', val: state.assets.cash, accent: 'var(--color-asset-accent-cash)', note: '统一可用现金池', deltaDay: '-56.50', deltaMonth: '-1,800', wide: true },
      { key: 'funds', name: '基金', val: fundTotal(), accent: 'var(--color-asset-accent-fund)', note: `含待确认 ¥${fmt(state.assets.fundPending, 0)}`, deltaDay: '+613.80', deltaMonth: '+10,200' },
      { key: 'stocks', name: '股票', val: state.assets.stock, accent: 'var(--color-asset-accent-stock)', note: 'A 股与场内 ETF', deltaDay: '+164.00', deltaMonth: '+1,500' },
      { key: 'provident', name: '公积金', val: state.assets.provident, accent: 'var(--color-asset-accent-provident)', note: '本月缴存 +5,200', deltaDay: '0.00', deltaMonth: '+3,200' },
      { key: 'gold', name: '贵金属', val: goldVal(), accent: 'var(--color-asset-accent-gold)', note: `持有 ${state.assets.goldGrams.toFixed(4)} 克`, deltaDay: '0.00', deltaMonth: '+200' }
    ];

    document.getElementById('screen-home').innerHTML = `
      <section class="hero">
        <div class="hero-top">
          <h1 class="hero-title">总览</h1>
          <div class="hero-right">
            <span class="hero-date">11月19日</span>
            <button type="button" class="hero-icon-btn" id="sync-quotes" aria-label="同步行情">
              <svg viewBox="0 0 24 24"><path d="M20 12a8 8 0 1 1-2.3-5.6"/><path d="M20 4v5h-5"/></svg>
            </button>
          </div>
        </div>
        <div class="hero-label-row">
          <span class="hero-label">总资产 <span class="hero-tag">CNY</span></span>
          <button type="button" class="eye-btn" id="toggle-mask" aria-label="切换金额显示">${eyeSvg(state.amountsMasked)}</button>
        </div>
        <div class="hero-amount tabular">
          ${state.amountsMasked ? '¥••••' : `¥${parts[0]}<span class="cents">.${parts[1]}</span>`}
        </div>
        <div class="hero-change-row">
          <button type="button" class="change-pill" id="open-change-detail">
            <span>${state.changeScope === 'day' ? '当日变化' : '本月变化'}</span>
            <span class="${changeVal >= 0 ? 'up' : 'down'} tabular">${mask(signed(changeVal))}</span>
            <span>${mask(changePct)}</span>
            <span>›</span>
          </button>
          <div class="hero-seg">
            <button type="button" data-change-scope="day" class="${state.changeScope === 'day' ? 'active' : ''}">当日</button>
            <button type="button" data-change-scope="month" class="${state.changeScope === 'month' ? 'active' : ''}">本月</button>
          </div>
        </div>
        ${state.changeScope === 'day' ? '<div class="hero-subnote">含 11月18日 基金净值收益</div>' : ''}
        <div class="trend-wrap">
          <div class="trend-readout">
            <span>2026-${tr.date}</span>
            <b class="tabular">${mask('¥' + fmt(tr.val))}</b>
          </div>
          ${tr.svg}
          <div class="trend-ranges">
            ${[['week','7日'],['month','1月'],['quarter','3月'],['all','全部']].map(([k,l]) => `<button type="button" data-range="${k}" class="${state.trendRange === k ? 'active' : ''}">${l}</button>`).join('')}
          </div>
        </div>
      </section>

      <div class="page-body">
        <section class="card" id="home-flow-card">
          <div class="section-head">
            <h2>本月收支</h2>
            <button type="button" class="section-link" data-jump="ledger">账单明细 ›</button>
          </div>
          <div class="flow-top">
            <div class="flow-col">
              <span><i class="flow-dot income"></i>本月收入</span>
              <b class="tabular">${mask('¥' + fmt(inc))}</b>
            </div>
            <div class="flow-col right">
              <span>本月支出<i class="flow-dot expense"></i></span>
              <b class="tabular">${mask('¥' + fmt(exp))}</b>
            </div>
          </div>
          <div class="tug-bar">
            <span class="tug-seg income" style="width:${incW.toFixed(1)}%"></span>
            <span class="tug-seg expense" style="width:${expW.toFixed(1)}%"></span>
          </div>
          <div class="flow-foot">
            <span>本月生活结余</span>
            <b class="tabular">${mask('¥' + fmt(inc - exp))}</b>
          </div>
        </section>

        <section>
          <div class="section-head">
            <h2>资产分布</h2>
            <span style="font-size:12px;color:var(--color-text-secondary);font-weight:600">5 类资产已覆盖</span>
          </div>
          <div class="asset-cards-grid">
            ${cards.map(c => {
              const share = total > 0 ? (c.val / total) * 100 : 0;
              return `<button type="button" class="asset-card ${c.wide ? 'wide' : ''}" data-jump="${c.key}" style="--accent:${c.accent};--share:${state.amountsMasked ? 18 : share.toFixed(1)}">
                <div class="asset-card-glow"><svg viewBox="0 0 200 120"><path d="M10,110 C60,40 120,90 190,15 L200,120 Z" fill="${c.accent}"/></svg></div>
                <div class="asset-card-veil"></div>
                <div class="asset-card-inner">
                  <div class="asset-card-top">
                    <span class="asset-badge"><i class="asset-dot"></i>${c.name}</span>
                    <span class="asset-share tabular">${mask(share.toFixed(1) + '%')}</span>
                  </div>
                  <div class="asset-card-val tabular">${mask('¥' + fmt(c.val))}</div>
                  <div class="asset-card-meta">${c.note}</div>
                  <div class="asset-card-foot">
                    <span class="tabular">${state.changeScope === 'day' ? '较昨日 ' + mask(c.deltaDay) : '较上月 ' + mask(c.deltaMonth)}</span>
                    <span>详情 ›</span>
                  </div>
                </div>
              </button>`;
            }).join('')}
          </div>
        </section>
      </div>
    `;

    document.getElementById('toggle-mask').onclick = () => { state.amountsMasked = !state.amountsMasked; render(); };
    document.getElementById('sync-quotes').onclick = (e) => {
      const btn = e.currentTarget;
      btn.classList.add('spinning');
      setTimeout(() => { btn.classList.remove('spinning'); toast('已同步基金净值、A股行情与上金所参考报价'); }, 650);
    };
    document.querySelectorAll('[data-change-scope]').forEach(b => b.onclick = () => { state.changeScope = b.dataset.changeScope; renderHome(); });
    document.querySelectorAll('[data-range]').forEach(b => b.onclick = () => { state.trendRange = b.dataset.range; state.trendIdx = null; renderHome(); });
    document.querySelectorAll('[data-jump]').forEach(b => b.onclick = () => go(b.dataset.jump));
    document.getElementById('open-change-detail').onclick = openChangeSheet;
    document.getElementById('home-trend-svg').onclick = (e) => {
      const rect = e.currentTarget.getBoundingClientRect();
      const ratio = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
      state.trendIdx = Math.round(ratio * (pts.length - 1));
      renderHome();
    };
  }

  function openChangeSheet() {
    if (state.changeScope === 'day') {
      openSheet('当日变化明细（11月19日）', `
        <div class="sheet-group-title">资产增加</div>
        <div class="form-group">
          <div class="row-item" style="display:block">
            <div style="display:flex;justify-content:space-between;align-items:center">
              <b>基金盈亏（含11-18净值）</b><b class="gain tabular">${mask('+613.80')}</b>
            </div>
            <details style="margin-top:6px">
              <summary style="font-size:12px;color:var(--color-text-link);cursor:pointer">展开估值计算拆解</summary>
              <div class="sheet-detail-box tabular">
                <div><span>今日估值</span><b>${mask('168,000.00')}</b></div>
                <div><span>扣除昨日估值</span><b>${mask('-167,086.20')}</b></div>
                <div><span>对应现金变动（定投扣款）</span><b>${mask('-300.00')}</b></div>
                <div><span>扣除新增录入 / 持仓核对</span><b>0.00</b></div>
              </div>
            </details>
          </div>
          <div class="row-item">
            <div class="row-copy"><b>股票盈亏</b><small>A 股与场内 ETF 当日市值变化</small></div>
            <div class="row-right"><b class="gain tabular">${mask('+164.00')}</b></div>
          </div>
        </div>
        <div class="sheet-group-title">资产减少</div>
        <div class="form-group">
          <div class="row-item">
            <div class="row-copy"><b>现金支出</b><small>仅统计影响现金余额的账单</small></div>
            <div class="row-right"><b class="loss tabular">${mask('-56.50')}</b></div>
          </div>
        </div>
        <div class="sheet-group-title">持平或暂无数据</div>
        <div class="form-group">
          <div class="row-item"><div class="row-copy"><b>公积金 / 贵金属 / 核对差额</b></div><div class="row-right"><b>0.00</b></div></div>
        </div>
      `);
    } else {
      openSheet('本月变化构成（同口径）', `
        <div class="form-group">
          <div class="row-item"><div class="row-copy"><b>本月市值变动</b><small>基金 +1,200 · 股票 +1,500 · 贵金属 +200</small></div><div class="row-right"><b class="gain tabular">${mask('+2,900.00')}</b></div></div>
          <div class="row-item"><div class="row-copy"><b>本月净存入 · 转账</b><small>生活结余 +5,200 · 公积金缴存 +5,200</small></div><div class="row-right"><b class="tabular">${mask('+10,400.00')}</b></div></div>
          <div class="row-item"><div class="row-copy"><b>本月新增记录</b><small>单列，不计入本月变化</small></div><div class="row-right"><b class="tabular">0.00</b></div></div>
        </div>
      `);
    }
  }

  function renderLedger() {
    const exp = state.monthFlow.expense;
    const budget = state.monthlyBudget;
    const pct = budget > 0 ? Math.min(100, (exp / budget) * 100) : 0;
    const remain = budget - exp;
    const dash = 113.1;
    const offset = dash * (1 - pct / 100);

    let bodyHtml = '';
    if (state.ledgerView === 'calendar') {
      const cells = [];
      for (let d = 1; d <= 30; d++) {
        const dayRows = state.entries.filter(e => e.day === d);
        const dayExp = dayRows.filter(e => e.kind === 'expense').reduce((s, e) => s + e.amount, 0);
        const dayInc = dayRows.filter(e => e.kind === 'income').reduce((s, e) => s + e.amount, 0);
        const sub = dayInc > 0 ? `<small class="pos">+${Math.round(dayInc)}</small>` : dayExp > 0 ? `<small class="neg">-${Math.round(dayExp)}</small>` : '';
        cells.push(`<button type="button" class="cal-cell ${state.selectedCalDay === d ? 'active' : ''}" data-cal-day="${d}"><span>${d}</span>${sub}</button>`);
      }
      const picked = state.entries.filter(e => e.day === state.selectedCalDay);
      bodyHtml = `
        <section class="card">
          <div class="section-head" style="margin-bottom:4px">
            <h2>2026年11月</h2>
            <span style="font-size:12px;color:var(--color-text-secondary)">点击日期查看流水</span>
          </div>
          <div class="cal-grid">
            ${['日','一','二','三','四','五','六'].map(w => `<span class="cal-week">${w}</span>`).join('')}
            ${cells.join('')}
          </div>
        </section>
        <section>
          <div class="day-head"><span>11月${state.selectedCalDay}日 明细</span><span>${picked.length} 笔</span></div>
          <div class="group-card">
            ${picked.length ? picked.map(e => `
              <div class="row-item">
                <div class="row-left">
                  <span class="cat-icon" data-tone="${e.tone}">${e.cat.slice(0,1)}</span>
                  <div class="row-copy"><b>${e.cat}</b><small>${e.group} · ${e.note || '无备注'}</small></div>
                </div>
                <div class="row-right"><b class="tabular ${e.kind === 'income' ? 'income-txt' : ''}">${e.kind === 'income' ? '+' : '-'}${fmt(e.amount)}</b></div>
              </div>`).join('') : '<div style="padding:24px;text-align:center;font-size:13px;color:var(--color-text-secondary)">当日暂无收支记录</div>'}
          </div>
        </section>
      `;
    } else if (state.ledgerView === 'list') {
      const days = [...new Set(state.entries.map(e => e.date))].sort().reverse();
      bodyHtml = days.map(dt => {
        const list = state.entries.filter(e => e.date === dt);
        return `<div>
          <div class="day-head"><span>${dt}</span><span>${list.length} 笔</span></div>
          <div class="group-card">
            ${list.map(e => `
              <div class="row-item">
                <div class="row-left">
                  <span class="cat-icon" data-tone="${e.tone}">${e.cat.slice(0,1)}</span>
                  <div class="row-copy"><b>${e.cat} ${e.auto ? '<span class="tag-soft">自动</span>' : ''}</b><small>${e.group} · ${e.note || '无备注'}</small></div>
                </div>
                <div class="row-right"><b class="tabular ${e.kind === 'income' ? 'income-txt' : ''}">${e.kind === 'income' ? '+' : '-'}${fmt(e.amount)}</b></div>
              </div>`).join('')}
          </div>
        </div>`;
      }).join('');
    } else {
      const rows = state.entries.filter(e => e.kind === state.ledgerChartKind);
      const map = {};
      rows.forEach(r => {
        const k = state.ledgerChartLevel === 'top' ? r.group : r.cat;
        map[k] = (map[k] || 0) + r.amount;
      });
      const sum = Math.max(Object.values(map).reduce((a, b) => a + b, 0), 1);
      const ranked = Object.entries(map).sort((a, b) => b[1] - a[1]);
      bodyHtml = `
        <section class="card">
          <div style="display:flex;justify-content:space-between;gap:8px;margin-bottom:14px">
            <div class="seg-control two" style="flex:1">
              <button type="button" data-ck="expense" class="${state.ledgerChartKind === 'expense' ? 'active' : ''}">支出</button>
              <button type="button" data-ck="income" class="${state.ledgerChartKind === 'income' ? 'active' : ''}">收入</button>
            </div>
            <div class="seg-control two" style="flex:1">
              <button type="button" data-cl="top" class="${state.ledgerChartLevel === 'top' ? 'active' : ''}">一级分类</button>
              <button type="button" data-cl="sub" class="${state.ledgerChartLevel === 'sub' ? 'active' : ''}">二级分类</button>
            </div>
          </div>
          <div style="font-size:13px;color:var(--color-text-secondary);margin-bottom:10px">
            本月${state.ledgerChartKind === 'expense' ? '支出' : '收入'}合计 <b class="tabular" style="color:var(--color-text-primary)">¥${fmt(sum)}</b> · 日均 ¥${fmt(sum / 19)}
          </div>
          ${ranked.map(([name, val]) => {
            const p = (val / sum) * 100;
            return `<div class="rank-item">
              <div class="rank-top"><span>${name} <small style="color:var(--color-text-secondary)">${p.toFixed(1)}%</small></span><b class="tabular">¥${fmt(val)}</b></div>
              <div class="rank-bar"><i style="width:${p.toFixed(1)}%"></i></div>
            </div>`;
          }).join('')}
        </section>
      `;
    }

    document.getElementById('screen-ledger').innerHTML = `
      <header class="sub-header">
        <h1>11月账单</h1>
        <button type="button" class="pill-action" id="go-cats">分类管理</button>
      </header>
      <div class="page-body" style="padding-top:4px">
        <section class="card">
          <div class="budget-row">
            <div class="budget-main">
              <span>本月支出</span>
              <b class="tabular">¥${fmt(exp)}</b>
              <div class="budget-sub">剩余可花 ¥${fmt(Math.max(remain, 0))} · 日均 ¥${fmt(Math.max(remain, 0) / 11)}</div>
            </div>
            <button type="button" class="budget-ring-btn" id="edit-budget">
              <svg class="budget-ring" viewBox="0 0 44 44">
                <circle class="track" cx="22" cy="22" r="18"/>
                <circle class="prog" cx="22" cy="22" r="18" stroke-dasharray="${dash}" stroke-dashoffset="${offset.toFixed(1)}"/>
              </svg>
              <div class="budget-ring-text">
                <small>月度预算</small>
                <b class="tabular">${pct.toFixed(0)}%</b>
              </div>
            </button>
          </div>
        </section>

        <div class="seg-control">
          <button type="button" data-lv="calendar" class="${state.ledgerView === 'calendar' ? 'active' : ''}">收支日历</button>
          <button type="button" data-lv="list" class="${state.ledgerView === 'list' ? 'active' : ''}">账单列表</button>
          <button type="button" data-lv="chart" class="${state.ledgerView === 'chart' ? 'active' : ''}">收支图表</button>
        </div>

        ${bodyHtml}
      </div>
    `;

    document.getElementById('go-cats').onclick = () => go('categories');
    document.querySelectorAll('[data-lv]').forEach(b => b.onclick = () => { state.ledgerView = b.dataset.lv; renderLedger(); });
    document.querySelectorAll('[data-cal-day]').forEach(b => b.onclick = () => { state.selectedCalDay = Number(b.dataset.calDay); renderLedger(); });
    document.querySelectorAll('[data-ck]').forEach(b => b.onclick = () => { state.ledgerChartKind = b.dataset.ck; renderLedger(); });
    document.querySelectorAll('[data-cl]').forEach(b => b.onclick = () => { state.ledgerChartLevel = b.dataset.cl; renderLedger(); });
    document.getElementById('edit-budget').onclick = () => {
      openSheet('设置本月预算', `
        <div class="form-group">
          <div class="form-field"><label>11月预算（元）</label><input id="inp-budget" type="number" value="${state.monthlyBudget}"></div>
        </div>
        <button type="button" class="btn-primary" style="width:100%" id="save-budget">保存预算</button>
      `);
      document.getElementById('save-budget').onclick = () => {
        const v = Number(document.getElementById('inp-budget').value);
        if (v > 0) { state.monthlyBudget = v; closeSheet(); renderLedger(); toast('已更新本月预算'); }
      };
    };
  }

  function renderCategories() {
    document.getElementById('screen-categories').innerHTML = `
      <header class="sub-header compact">
        <button type="button" class="back-btn" id="cat-back"><svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></button>
        <h1>分类管理</h1>
        <button type="button" class="pill-action" id="cat-add">＋ 新增</button>
      </header>
      <div class="page-body" style="padding-top:4px">
        <div style="font-size:12px;color:var(--color-text-secondary)">两级分类统一在此维护；支持改名同步历史、停用保留旧统计及长按拖拽排序。</div>
        ${categoryGroups.expense.map(g => `
          <details class="plan-group" open>
            <summary><span>${g.name}（支出分组）</span><span class="tag-soft">${g.items.length} 项</span></summary>
            <div class="plan-group-body">
              ${g.items.map(it => `
                <div class="row-item">
                  <div class="row-left">
                    <span class="cat-icon" data-tone="${it.tone}">${it.icon}</span>
                    <div class="row-copy"><b>${it.cat}</b><small>一级分组：${g.name}</small></div>
                  </div>
                  <span style="color:var(--color-text-secondary);font-size:16px">≡</span>
                </div>`).join('')}
            </div>
          </details>`).join('')}
      </div>
    `;
    document.getElementById('cat-back').onclick = () => go(state.prevPage === 'entry' ? 'entry' : 'ledger');
    document.getElementById('cat-add').onclick = () => toast('原型演示：正式版支持新增/拖拽与跨一级分组移动');
  }

  function evalExpr(expr) {
    try {
      const clean = expr.replace(/×/g, '*').replace(/÷/g, '/').replace(/−/g, '-').replace(/[^0-9.+\-*/]/g, '');
      if (!clean) return 0;
      const val = Function(`"use strict";return (${clean})`)();
      return Number.isFinite(val) && val > 0 ? Math.round(val * 100) / 100 : 0;
    } catch { return 0; }
  }

  function renderEntry() {
    const groups = categoryGroups[state.entry.kind];
    document.getElementById('screen-entry').innerHTML = `
      <header class="entry-top">
        <button type="button" class="section-link" id="entry-cancel">取消</button>
        <div class="seg-control two" style="width:150px">
          <button type="button" data-ek="expense" class="${state.entry.kind === 'expense' ? 'active' : ''}">支出</button>
          <button type="button" data-ek="income" class="${state.entry.kind === 'income' ? 'active' : ''}">收入</button>
        </div>
        <button type="button" class="section-link" id="entry-cats">分类</button>
      </header>
      <div class="entry-cats">
        ${groups.map(g => `
          <div>
            <p class="cat-group-title">${g.name}</p>
            <div class="cat-grid">
              ${g.items.map(it => `
                <button type="button" class="cat-tile ${state.entry.category === it.cat ? 'active' : ''}" data-pick-cat="${it.cat}" data-pick-group="${g.name}" data-pick-tone="${it.tone}">
                  <span class="cat-icon" data-tone="${it.tone}">${it.icon}</span>
                  <span>${it.cat}</span>
                </button>`).join('')}
            </div>
          </div>`).join('')}
      </div>
      <div class="entry-dock">
        <div class="entry-display">
          <span class="tag-soft">${state.entry.group} · ${state.entry.category}</span>
          <div class="entry-expr tabular">¥${state.entry.expr}</div>
        </div>
        <div class="entry-note-row">
          <input id="entry-note" placeholder="添加备注（如 午餐）" value="${state.entry.note}">
        </div>
        <div class="keypad">
          ${['7','8','9','÷','4','5','6','×','1','2','3','−','0','00','.','＋'].map(k => `<button type="button" class="${'÷×−＋'.includes(k) ? 'op' : ''}" data-key="${k}">${k}</button>`).join('')}
        </div>
        <div class="keypad" style="margin-top:7px;grid-template-columns:1fr 1fr 1fr 1fr">
          <button type="button" data-key="C">清空</button>
          <button type="button" data-key="DEL">⌫</button>
          <button type="button" class="op" data-key="=" style="grid-column:span 2">＝ 计算</button>
        </div>
        <div class="entry-submit-row">
          <button type="button" class="btn-secondary" id="entry-again">保存后再记</button>
          <button type="button" class="btn-primary" id="entry-done">完成</button>
        </div>
      </div>
    `;

    document.getElementById('entry-cancel').onclick = () => go(state.prevPage || 'home');
    document.getElementById('entry-cats').onclick = () => go('categories');
    document.querySelectorAll('[data-ek]').forEach(b => b.onclick = () => {
      state.entry.kind = b.dataset.ek;
      const firstG = categoryGroups[state.entry.kind][0];
      state.entry.group = firstG.name;
      state.entry.category = firstG.items[0].cat;
      state.entry.tone = firstG.items[0].tone;
      renderEntry();
    });
    document.querySelectorAll('[data-pick-cat]').forEach(b => b.onclick = () => {
      state.entry.category = b.dataset.pickCat;
      state.entry.group = b.dataset.pickGroup;
      state.entry.tone = b.dataset.pickTone;
      renderEntry();
    });
    document.getElementById('entry-note').oninput = (e) => { state.entry.note = e.target.value; };
    document.querySelectorAll('[data-key]').forEach(b => b.onclick = () => {
      const k = b.dataset.key;
      if (k === 'C') state.entry.expr = '0';
      else if (k === 'DEL') state.entry.expr = state.entry.expr.length > 1 ? state.entry.expr.slice(0, -1) : '0';
      else if (k === '=') state.entry.expr = String(evalExpr(state.entry.expr) || 0);
      else state.entry.expr = state.entry.expr === '0' && !'.＋−×÷'.includes(k) ? k : state.entry.expr + k;
      renderEntry();
    });

    const save = (stay) => {
      const val = evalExpr(state.entry.expr);
      if (val <= 0) { toast('请输入大于 0 的金额'); return; }
      if (state.entry.kind === 'expense' && val > state.assets.cash) { toast('现金余额不足，请先核对现金'); return; }
      state.entries.unshift({
        id: Date.now(), day: 19, date: '2026-11-19', kind: state.entry.kind,
        group: state.entry.group, cat: state.entry.category, tone: state.entry.tone,
        amount: val, note: state.entry.note || '记一笔演示'
      });
      if (state.entry.kind === 'expense') {
        state.assets.cash -= val;
        state.monthFlow.expense += val;
      } else {
        state.assets.cash += val;
        state.monthFlow.income += val;
      }
      state.entry.expr = '0';
      state.entry.note = '';
      toast(`已记一笔 ¥${fmt(val)}，现金已同步联动`);
      if (stay) renderEntry();
      else go('ledger');
    };
    document.getElementById('entry-again').onclick = () => save(true);
    document.getElementById('entry-done').onclick = () => save(false);
  }

  function renderCapital() {
    const total = totalAssets();
    const goal = state.capitalGoalWan * 10000;
    const pct = Math.min(100, (total / goal) * 100);
    const gap = Math.max(0, goal - total);
    const monthlySave = state.salary.netMonthly - state.monthFlow.expense + state.salary.providentMonthly;

    document.getElementById('screen-capital').innerHTML = `
      <header class="sub-header">
        <h1>目标</h1>
        <button type="button" class="circle-btn" id="open-assumptions" aria-label="调整假设">
          <svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.6 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.6a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9c.26.6.85 1 1.51 1H21a2 2 0 1 1 0 4h-.09c-.66 0-1.25.4-1.51 1z"/></svg>
        </button>
      </header>
      <div style="padding:0 20px 14px">
        <div class="seg-control two">
          <button type="button" class="active">资本积累</button>
          <button type="button" id="tab-plans">自动计划</button>
        </div>
      </div>

      <section class="hero" style="margin:0 16px;border-radius:26px">
        <div class="hero-top">
          <span class="hero-label">当前总资产 · 目标 ${state.capitalGoalWan} 万</span>
          <button type="button" class="eye-btn" id="cap-eye">${eyeSvg(state.amountsMasked)}</button>
        </div>
        <div class="hero-amount tabular" style="font-size:34px;margin:6px 0 4px">${mask('¥' + fmt(total))}</div>
        <div style="font-size:12px;color:var(--color-hero-text-secondary)">距目标还差 <b class="tabular" style="color:#fff">${mask('¥' + fmt(gap))}</b> · 预计 <b style="color:var(--color-accent-mint)">2031年04月</b> 达成</div>

        <div class="hero-two-col">
          <div class="hero-metric"><small>月均积累（不含年终奖）</small><b class="tabular">${mask('¥' + fmt(monthlySave, 0))}</b></div>
          <div class="hero-metric"><small>组合综合年化</small><b class="tabular">${(state.capitalAnnualReturn * 0.72 + 1.5 * 0.2).toFixed(1)}%</b></div>
        </div>

        <div class="glass-track-wrap">
          <div class="glass-track-labels">
            <span>2026.11（当前 ${mask(pct.toFixed(1) + '%')}）</span>
            <span>终点 ${state.capitalGoalWan}万</span>
          </div>
          <div class="glass-track">
            <div class="glass-fill" style="width:${state.amountsMasked ? 30 : pct.toFixed(1)}%"></div>
            <span class="glass-star" style="left:10%" title="10万里程碑">✦</span>
            <span class="glass-star" style="left:20%" title="20万里程碑">✦</span>
            <span class="glass-star" style="left:30%" title="30万里程碑">✦</span>
            <span class="glass-cursor" style="left:${state.amountsMasked ? 30 : pct.toFixed(1)}%"></span>
            <span class="glass-end"></span>
          </div>
        </div>
      </section>

      <div class="page-body">
        <section class="card">
          <div class="section-head">
            <h2>年度积累推演</h2>
            <span class="tag-soft">预测</span>
          </div>
          <table class="year-table">
            <thead><tr><th>年份</th><th>年末资本</th><th>净储蓄*</th><th>投资收益</th></tr></thead>
            <tbody>
              <tr><td>2027</td><td>${mask('52.1万')}</td><td>${mask('+15.5万')}</td><td>${mask('+2.8万')}</td></tr>
              <tr><td>2028</td><td>${mask('71.4万')}</td><td>${mask('+15.5万')}</td><td>${mask('+3.8万')}</td></tr>
              <tr><td>2029</td><td>${mask('92.1万')}</td><td>${mask('+15.5万')}</td><td>${mask('+5.2万')}</td></tr>
              <tr><td>2030</td><td>${mask('114.2万')}</td><td>${mask('+15.5万')}</td><td>${mask('+6.6万')}</td></tr>
            </tbody>
          </table>
          <div style="margin-top:8px;font-size:11px;color:var(--color-text-secondary)">* 净储蓄含年终奖（${state.salary.bonusMonth}月发放 ${mask('¥' + fmt(state.salary.bonusNet, 0))}）与公积金双边缴存。</div>
        </section>

        <section class="card">
          <div class="section-head"><h2>本月实际对照</h2><span class="tag-ok tag-soft">达标</span></div>
          <div style="display:flex;justify-content:space-between;font-size:13px">
            <span style="color:var(--color-text-secondary)">本月实际净积累</span>
            <b class="tabular">${mask('¥10,400.00')}</b>
          </div>
          <div style="display:flex;justify-content:space-between;font-size:13px;margin-top:8px">
            <span style="color:var(--color-text-secondary)">假设预期净积累</span>
            <b class="tabular">${mask('¥' + fmt(monthlySave))}</b>
          </div>
        </section>
      </div>
    `;

    document.getElementById('tab-plans').onclick = () => go('plans');
    document.getElementById('cap-eye').onclick = () => { state.amountsMasked = !state.amountsMasked; renderCapital(); };
    document.getElementById('open-assumptions').onclick = () => {
      openSheet('目标与推演假设', `
        <div class="form-group">
          <div class="form-field"><label>总资产目标（万元）</label><input id="cap-goal-inp" type="number" value="${state.capitalGoalWan}"></div>
          <div class="form-field"><label>收入/年终奖来源</label>
            <select id="cap-src-sel">
              <option value="profile" ${state.useSalaryProfile ? 'selected' : ''}>跟随工资档案</option>
              <option value="manual" ${!state.useSalaryProfile ? 'selected' : ''}>手动覆盖</option>
            </select>
          </div>
          <div class="form-field"><label>月收入（税后）</label><input value="${state.salary.netMonthly}" ${state.useSalaryProfile ? 'readonly' : ''}></div>
          <div class="form-field"><label>年终奖（${state.salary.bonusMonth}月到手）</label><input value="${state.salary.bonusNet}" ${state.useSalaryProfile ? 'readonly' : ''}></div>
          <div class="form-field"><label>投资年化（%）</label><input id="cap-ret-inp" type="number" step="0.1" value="${state.capitalAnnualReturn}"></div>
        </div>
        <div style="display:flex;gap:8px;margin-bottom:14px">
          <button type="button" class="pill-action" id="fill-ndx" style="flex:1">纳指20年 14.2%</button>
          <button type="button" class="pill-action" id="fill-spx" style="flex:1">标普20年 9.8%</button>
        </div>
        <button type="button" class="btn-primary" style="width:100%" id="save-cap">保存并重算</button>
      `);
      document.getElementById('fill-ndx').onclick = () => { document.getElementById('cap-ret-inp').value = 14.2; };
      document.getElementById('fill-spx').onclick = () => { document.getElementById('cap-ret-inp').value = 9.8; };
      document.getElementById('save-cap').onclick = () => {
        state.capitalGoalWan = Number(document.getElementById('cap-goal-inp').value) || 100;
        state.capitalAnnualReturn = Number(document.getElementById('cap-ret-inp').value) || 7.5;
        state.useSalaryProfile = document.getElementById('cap-src-sel').value === 'profile';
        closeSheet();
        renderCapital();
        toast('已重新推演目标达成时间');
      };
    };
  }

  function renderPlans() {
    const groups = ['基金定投', '公积金', '工资收入'];
    const calCells = [];
    for (let d = 1; d <= 30; d++) {
      const isUsHoliday = d === 26;
      calCells.push(`<div class="cal-cell ${d === 19 ? 'active' : ''}"><span>${d}</span>${isUsHoliday ? '<span class="holiday-dot" title="感恩节美股休市，三地定投顺延"></span>' : ''}</div>`);
    }

    document.getElementById('screen-plans').innerHTML = `
      <header class="sub-header">
        <h1>目标</h1>
        <button type="button" class="pill-action" id="run-plans">立即执行</button>
      </header>
      <div style="padding:0 20px 14px">
        <div class="seg-control two">
          <button type="button" id="tab-capital">资本积累</button>
          <button type="button" class="active">自动计划</button>
        </div>
      </div>

      <div class="page-body" style="padding-top:2px">
        ${groups.map(g => {
          const items = state.plans.filter(p => p.group === g);
          return `<details class="plan-group" open>
            <summary><span>${g}</span><span class="tag-soft">${items.length} 条</span></summary>
            <div class="plan-group-body">
              ${items.map(p => `
                <div class="row-item">
                  <div class="row-copy">
                    <b>${p.name} ${p.managed ? '<span class="tag-soft">工资档案</span>' : ''}</b>
                    <small>${p.rule}${p.paused ? ' · 已暂停' : ''}</small>
                  </div>
                  <div class="row-right">
                    <b class="tabular">¥${fmt(p.amount, 0)}</b>
                    <button type="button" class="section-link" data-toggle-plan="${p.id}" style="font-size:11px">${p.paused ? '恢复' : '暂停'}</button>
                  </div>
                </div>`).join('')}
            </div>
          </details>`;
        }).join('')}

        <section class="card">
          <div class="section-head"><h2>2026年11月 · 三地交易日历</h2><span class="tag-soft tag-warn">11/26 美股休市</span></div>
          <div class="cal-grid">
            ${['日','一','二','三','四','五','六'].map(w => `<span class="cal-week">${w}</span>`).join('')}
            ${calCells.join('')}
          </div>
          <div style="margin-top:10px;font-size:12px;color:var(--color-text-secondary)">黄点标注因美国或香港休市而顺延的三地定投日期（如 11 月 26 日感恩节）。</div>
        </section>

        <details class="plan-group">
          <summary><span>最近执行记录</span><span class="tag-soft">10条/页</span></summary>
          <div class="plan-group-body">
            <div class="row-item"><div class="row-copy"><b>摩根纳斯达克100 C 定投</b><small>2026-11-19 14:30 · 已记账</small></div><div class="row-right"><b class="tabular">¥300.00</b></div></div>
            <div class="row-item"><div class="row-copy"><b>每月税后工资</b><small>2026-11-15 08:30 · 已记账</small></div><div class="row-right"><b class="income-txt tabular">+12,000.00</b></div></div>
          </div>
        </details>
      </div>
    `;

    document.getElementById('tab-capital').onclick = () => go('capital');
    document.getElementById('run-plans').onclick = () => toast('已检查到期计划：当前无待补跑期次');
    document.querySelectorAll('[data-toggle-plan]').forEach(b => b.onclick = () => {
      const p = state.plans.find(x => x.id === b.dataset.togglePlan);
      p.paused = !p.paused;
      renderPlans();
      toast(p.paused ? `已暂停「${p.name}」` : `已恢复「${p.name}」`);
    });
  }

  function renderMine() {
    const themeLabel = { system: '跟随系统', light: '亮色 · Daylight Prism', dark: '暗色 · Obsidian Mint' }[state.themePref];
    document.getElementById('screen-mine').innerHTML = `
      <header class="sub-header"><h1>我的</h1></header>
      <div class="page-body" style="padding-top:4px">
        <div>
          <div class="sheet-group-title">个人信息</div>
          <div class="group-card">
            <div class="row-item">
              <div class="row-left">
                <span class="cat-icon" data-tone="violet" style="border-radius:50%;width:44px;height:44px">LU</span>
                <div class="row-copy"><b>LifeUp</b><small>个人单账本 · 本地头像与昵称</small></div>
              </div>
              <span class="tag-soft">自用</span>
            </div>
            <button type="button" class="row-item" id="go-salary">
              <div class="row-copy"><b>工资档案</b><small>维护税后月薪、公积金与年终奖，联动三项计划</small></div>
              <div class="row-right"><b>查看 ›</b></div>
            </button>
          </div>
        </div>

        <div>
          <div class="sheet-group-title">个性化设置</div>
          <div class="group-card">
            <button type="button" class="row-item" id="open-theme-sheet">
              <div class="row-copy"><b>主题配色</b><small>Midnight Mint v2.1 双主题</small></div>
              <div class="row-right"><b style="color:var(--color-brand)">${themeLabel} ›</b></div>
            </button>
          </div>
        </div>

        <div>
          <div class="sheet-group-title">数据管理</div>
          <div class="group-card">
            <button type="button" class="row-item" id="go-backup">
              <div class="row-copy"><b>数据导出与备份</b><small>明细 CSV · 完整备份 JSON（版本 V5）</small></div>
              <div class="row-right"><b>进入 ›</b></div>
            </button>
          </div>
        </div>
      </div>
    `;
    document.getElementById('go-salary').onclick = () => go('salary');
    document.getElementById('go-backup').onclick = () => go('backup');
    document.getElementById('open-theme-sheet').onclick = () => {
      openSheet('主题配色', `
        <div class="form-group">
          ${[['system','跟随系统','按设备外观自动切换'],['light','亮色（Daylight Prism）','冷瓷白底 + 晴空极光紫 Hero'],['dark','暗色（Obsidian Mint）','冷曜石黑 + 星夜靛紫 Hero']].map(([k,t,d]) => `
            <button type="button" class="row-item" data-pick-theme="${k}">
              <div class="row-copy"><b>${t}</b><small>${d}</small></div>
              <div class="row-right">${state.themePref === k ? '<span class="tag-soft">当前</span>' : ''}</div>
            </button>`).join('')}
        </div>
      `);
      document.querySelectorAll('[data-pick-theme]').forEach(b => b.onclick = () => {
        applyTheme(b.dataset.pickTheme);
        closeSheet();
        renderMine();
      });
    };
  }

  function renderSalary() {
    const s = state.salary;
    const annualCash = s.netMonthly * 12 + s.bonusNet;
    const annualPf = s.providentMonthly * 12;
    const sm = (v) => state.salaryMasked ? '••••' : '¥' + fmt(v, 0);

    document.getElementById('screen-salary').innerHTML = `
      <header class="sub-header compact">
        <button type="button" class="back-btn" id="sal-back"><svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></button>
        <h1>工资档案</h1>
        <button type="button" class="pill-action" id="sal-toggle">${state.salaryMasked ? '显示金额' : '隐藏金额'}</button>
      </header>
      <div class="page-body" style="padding-top:4px">
        <section class="card">
          <div class="section-head"><h2>税后收入与年度积累</h2><span class="tag-soft">默认脱敏</span></div>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:14px">
            <div><small style="color:var(--color-text-secondary)">税后月薪</small><b class="tabular" style="display:block;font-size:20px;margin-top:4px">${sm(s.netMonthly)}</b></div>
            <div><small style="color:var(--color-text-secondary)">公积金月缴存</small><b class="tabular" style="display:block;font-size:20px;margin-top:4px">${sm(s.providentMonthly)}</b></div>
            <div><small style="color:var(--color-text-secondary)">年终奖（到手）</small><b class="tabular" style="display:block;font-size:20px;margin-top:4px">${sm(s.bonusNet)}</b></div>
            <div><small style="color:var(--color-text-secondary)">发薪日</small><b class="tabular" style="display:block;font-size:20px;margin-top:4px">每月 ${s.payDay} 日</b></div>
          </div>
          <div style="margin-top:16px;padding-top:14px;border-top:1px solid var(--color-border-subtle);display:flex;justify-content:space-between;font-size:13px">
            <span>年度到手现金 <b class="tabular">${sm(annualCash)}</b></span>
            <span>公积金年积累 <b class="tabular">${sm(annualPf)}</b></span>
          </div>
        </section>
        <button type="button" class="btn-secondary" style="width:100%" id="sal-plans">查看已同步的自动计划（工资 / 公积金 / 年终奖）›</button>
      </div>
    `;
    document.getElementById('sal-back').onclick = () => go('mine');
    document.getElementById('sal-toggle').onclick = () => { state.salaryMasked = !state.salaryMasked; renderSalary(); };
    document.getElementById('sal-plans').onclick = () => go('plans');
  }

  function renderBackup() {
    document.getElementById('screen-backup').innerHTML = `
      <header class="sub-header compact">
        <button type="button" class="back-btn" id="bk-back"><svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></button>
        <h1>数据导出与备份</h1>
        <span style="width:40px"></span>
      </header>
      <div class="page-body" style="padding-top:4px">
        <section class="card">
          <div class="section-head"><h2>完整备份（版本 V5）</h2><span class="tag-soft">兼容 V1–V5</span></div>
          <p style="margin:0 0 14px;font-size:13px;color:var(--color-text-secondary);line-height:1.6">包含五类资产（现金、基金、股票、公积金、贵金属）、两级分类、收支流水、行情观测、五类自动计划与期次、资本目标与工资档案。恢复前强制执行完整性与历史重放校验。</p>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px">
            <button type="button" class="btn-secondary" id="dl-csv">导出明细 CSV</button>
            <button type="button" class="btn-primary" id="dl-json">下载 JSON 备份</button>
          </div>
        </section>
      </div>
    `;
    document.getElementById('bk-back').onclick = () => go('mine');
    document.getElementById('dl-csv').onclick = () => toast('原型演示：正式版下载可核对明细 CSV');
    document.getElementById('dl-json').onclick = () => toast('原型演示：正式版下载完整 V5 JSON 备份');
  }

  function renderCash() {
    document.getElementById('screen-cash').innerHTML = `
      <section class="hero">
        <div class="hero-top">
          <button type="button" class="hero-icon-btn" id="cash-back"><svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></button>
          <span class="hero-label" style="margin:0;font-size:16px">现金</span>
          <button type="button" class="hero-icon-btn" id="cash-reconcile" style="width:auto;padding:0 12px;border-radius:99px;font-size:12px;font-weight:700">核对余额</button>
        </div>
        <div class="hero-amount tabular" style="margin-top:16px">${mask('¥' + fmt(state.assets.cash))}</div>
        <div class="hero-two-col" style="grid-template-columns:repeat(3,1fr)">
          <div class="hero-metric"><small>本月流入</small><b class="tabular" style="font-size:15px">${mask('14,000')}</b></div>
          <div class="hero-metric"><small>本月流出</small><b class="tabular" style="font-size:15px">${mask('15,800')}</b></div>
          <div class="hero-metric"><small>余额净变动</small><b class="tabular" style="font-size:15px">${mask('-1,800')}</b></div>
        </div>
      </section>
      <div class="page-body">
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px">
          <button type="button" class="btn-secondary" id="cash-exp">记支出</button>
          <button type="button" class="btn-primary" id="cash-inc">记收入</button>
        </div>
        <div class="group-card">
          ${state.cashMoves.map(m => `
            <div class="row-item">
              <div class="row-copy"><b>${m.title}</b><small>${m.date} · ${m.note}</small></div>
              <div class="row-right"><b class="tabular ${m.amount > 0 ? 'income-txt' : ''}">${signed(m.amount)}</b></div>
            </div>`).join('')}
        </div>
      </div>
    `;
    document.getElementById('cash-back').onclick = () => go('home');
    document.getElementById('cash-exp').onclick = () => { state.entry.kind = 'expense'; go('entry'); };
    document.getElementById('cash-inc').onclick = () => { state.entry.kind = 'income'; go('entry'); };
    document.getElementById('cash-reconcile').onclick = () => {
      openSheet('核对现金余额', `
        <div class="form-group">
          <div class="form-field"><label>当前账面</label><input value="¥${fmt(state.assets.cash)}" readonly></div>
          <div class="form-field"><label>实际余额</label><input id="rec-cash" type="number" value="${state.assets.cash}"></div>
          <div class="form-field"><label>调整原因</label><input id="rec-reason" placeholder="如 银行卡零钱差额"></div>
        </div>
        <button type="button" class="btn-primary" style="width:100%" id="save-rec">保存核对</button>
      `);
      document.getElementById('save-rec').onclick = () => {
        const next = Number(document.getElementById('rec-cash').value);
        const diff = next - state.assets.cash;
        if (Math.abs(diff) < 0.01) { closeSheet(); toast('余额一致，无需生成调整'); return; }
        state.assets.cash = next;
        state.cashMoves.unshift({ date: '2026-11-19', type: 'adj', title: '现金余额核对调整', amount: diff, note: document.getElementById('rec-reason').value || '手动核对（不计入生活收支）' });
        closeSheet();
        renderCash();
        toast(`已生成独立核对差额 ${signed(diff)}`);
      };
    };
  }

  function renderFunds() {
    const groups = ['全部', '纳斯达克', '标普500', '红利'];
    const list = state.fundGroup === '全部' ? state.funds : state.funds.filter(f => f.group === state.fundGroup);
    const total = list.reduce((s, f) => s + f.assetValue, 0);
    const dayP = list.reduce((s, f) => s + f.dayProfit, 0);
    const holdP = list.reduce((s, f) => s + (f.assetValue - f.pending - f.cost), 0);

    document.getElementById('screen-funds').innerHTML = `
      <section class="hero">
        <div class="hero-top">
          <button type="button" class="hero-icon-btn" id="fund-back"><svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></button>
          <span class="hero-label" style="margin:0;font-size:16px">投资（基金）</span>
          <span class="tag-soft" style="background:var(--color-hero-surface);color:#fff">☺ 3/3 已披露</span>
        </div>
        <div class="hero-amount tabular" style="margin-top:14px;font-size:36px">${mask('¥' + fmt(total))}</div>
        <div class="hero-two-col">
          <div class="hero-metric"><small>今日收益（含11-18净值）</small><b class="tabular ${dayP >= 0 ? 'gain' : 'loss'}" style="color:${dayP >= 0 ? 'var(--color-finance-gain-on-hero)' : 'var(--color-finance-loss-on-hero)'}">${mask(signed(dayP))}</b></div>
          <div class="hero-metric"><small>持有收益</small><b class="tabular" style="color:var(--color-finance-gain-on-hero)">${mask(signed(holdP))}</b></div>
        </div>
      </section>
      <div class="page-body">
        <div class="chips-row">
          ${groups.map(g => `<button type="button" class="chip ${state.fundGroup === g ? 'active' : ''}" data-fg="${g}">${g}</button>`).join('')}
        </div>
        <div class="seg-control" style="grid-template-columns:repeat(4,1fr)">
          ${[['holdings','持仓明细'],['returns','收益日历'],['trades','交易记录'],['plans','我的定投']].map(([k,l]) => `<button type="button" data-fv="${k}" class="${state.fundView === k ? 'active' : ''}" style="font-size:12px">${l}</button>`).join('')}
        </div>
        ${list.map(f => {
          const cum = (f.assetValue - f.pending - f.cost) + f.realized;
          const cumPct = (cum / f.cost) * 100;
          return `<details class="plan-group">
            <summary style="display:block">
              <div style="display:flex;justify-content:space-between;align-items:baseline;gap:8px">
                <span style="font-size:15px">${f.name}</span>
                <b class="tabular">${mask('¥' + fmt(f.assetValue))}</b>
              </div>
              <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin-top:10px;font-size:12px;font-weight:500">
                <div><small style="color:var(--color-text-secondary);display:block">日收益 ${f.dayPct > 0 ? '+' : ''}${f.dayPct}%</small><b class="tabular ${f.dayProfit >= 0 ? 'gain' : 'loss'}">${mask(signed(f.dayProfit))}</b></div>
                <div><small style="color:var(--color-text-secondary);display:block">累计收益</small><b class="tabular ${cum >= 0 ? 'gain' : 'loss'}">${mask(signed(cum))}</b></div>
                <div style="text-align:right"><small style="color:var(--color-text-secondary);display:block">累计收益率</small><b class="tabular ${cum >= 0 ? 'gain' : 'loss'}">${cumPct > 0 ? '+' : ''}${cumPct.toFixed(1)}%</b></div>
              </div>
            </summary>
            <div class="plan-group-body">
              ${f.classes.map(c => `
                <div class="row-item">
                  <div class="row-copy"><b>${c.cls} · ${c.code}</b><small>份额 ${c.shares.toFixed(2)} · 净值 ${c.nav.toFixed(4)} (${c.navDate})</small></div>
                  <div class="row-right"><b class="tabular">${mask('¥' + fmt(c.value))}</b></div>
                </div>`).join('')}
            </div>
          </details>`;
        }).join('')}
      </div>
    `;
    document.getElementById('fund-back').onclick = () => go('home');
    document.querySelectorAll('[data-fg]').forEach(b => b.onclick = () => { state.fundGroup = b.dataset.fg; renderFunds(); });
    document.querySelectorAll('[data-fv]').forEach(b => b.onclick = () => { state.fundView = b.dataset.fv; toast(`切换至「${b.textContent}」视图`); renderFunds(); });
  }

  function renderStocks() {
    const sorted = [...state.stocks].sort((a, b) => {
      if (state.stockSort === 'profit') return b.totalProfit - a.totalProfit;
      if (state.stockSort === 'name') return a.name.localeCompare(b.name, 'zh');
      return (b.shares * b.price) - (a.shares * a.price);
    });
    const dayP = sorted.reduce((s, x) => s + x.dayProfit, 0);
    const totalP = sorted.reduce((s, x) => s + x.totalProfit, 0);

    document.getElementById('screen-stocks').innerHTML = `
      <section class="hero">
        <div class="hero-top">
          <button type="button" class="hero-icon-btn" id="stk-back"><svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></button>
          <span class="hero-label" style="margin:0;font-size:16px">股票（A 股与 ETF）</span>
          <button type="button" class="hero-icon-btn" id="stk-sync" aria-label="刷新行情"><svg viewBox="0 0 24 24"><path d="M20 12a8 8 0 1 1-2.3-5.6"/><path d="M20 4v5h-5"/></svg></button>
        </div>
        <div class="hero-amount tabular" style="margin-top:14px;font-size:36px">${mask('¥' + fmt(state.assets.stock))}</div>
        <div class="hero-two-col">
          <div class="hero-metric"><small>今日盈亏</small><b class="tabular" style="color:var(--color-finance-gain-on-hero)">${mask(signed(dayP))}</b></div>
          <div class="hero-metric"><small>总收益</small><b class="tabular" style="color:var(--color-finance-gain-on-hero)">${mask(signed(totalP))}</b></div>
        </div>
      </section>
      <div class="page-body">
        <div class="seg-control">
          <button type="button" data-ss="value" class="${state.stockSort === 'value' ? 'active' : ''}">按市值</button>
          <button type="button" data-ss="profit" class="${state.stockSort === 'profit' ? 'active' : ''}">按总收益</button>
          <button type="button" data-ss="name" class="${state.stockSort === 'name' ? 'active' : ''}">按名称</button>
        </div>
        <div class="group-card">
          ${sorted.map(s => `
            <div class="row-item">
              <div class="row-copy">
                <b>${s.name} <span class="tag-soft">${s.market}</span></b>
                <small>${s.code} · ${s.shares}股 · 现价 ${s.price.toFixed(3)} · 均价 ${s.avgCost.toFixed(3)}</small>
              </div>
              <div class="row-right">
                <b class="tabular">${mask('¥' + fmt(s.shares * s.price))}</b>
                <small class="tabular ${s.dayProfit >= 0 ? 'gain' : 'loss'}">今日 ${mask(signed(s.dayProfit))}</small>
              </div>
            </div>`).join('')}
        </div>
      </div>
    `;
    document.getElementById('stk-back').onclick = () => go('home');
    document.getElementById('stk-sync').onclick = () => toast('已从东方财富行情源刷新最新价与昨收');
    document.querySelectorAll('[data-ss]').forEach(b => b.onclick = () => { state.stockSort = b.dataset.ss; renderStocks(); });
  }

  function renderProvident() {
    document.getElementById('screen-provident').innerHTML = `
      <section class="hero">
        <div class="hero-top">
          <button type="button" class="hero-icon-btn" id="pf-back"><svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></button>
          <span class="hero-label" style="margin:0;font-size:16px">公积金</span>
          <span style="width:38px"></span>
        </div>
        <div class="hero-amount tabular" style="margin-top:14px;font-size:36px">${mask('¥' + fmt(state.assets.provident))}</div>
        <div class="hero-two-col">
          <div class="hero-metric"><small>本月双边缴存</small><b class="tabular">+5,200.00</b></div>
          <div class="hero-metric"><small>本月提取至现金</small><b class="tabular">-2,000.00</b></div>
        </div>
      </section>
      <div class="page-body">
        <button type="button" class="btn-primary" style="width:100%" id="pf-action">缴存 · 提取 / 每月自动缴存</button>
        <div class="group-card">
          <div class="row-item"><div class="row-copy"><b>本月缴存（个人+单位）</b><small>2026-11-15 · 工资档案托管</small></div><div class="row-right"><b class="income-txt tabular">+5,200.00</b></div></div>
          <div class="row-item"><div class="row-copy"><b>公积金提取到现金</b><small>2026-11-15 · 等额转入现金池</small></div><div class="row-right"><b class="tabular">-2,000.00</b></div></div>
        </div>
      </div>
    `;
    document.getElementById('pf-back').onclick = () => go('home');
    document.getElementById('pf-action').onclick = () => {
      openSheet('公积金缴存 / 提取', `
        <div class="form-group">
          <div class="form-field"><label>提取到现金（元）</label><input id="pf-wd" type="number" value="2000"></div>
          <div class="form-field"><label>每月自动缴存</label><input value="¥${state.salary.providentMonthly}/月 · 已启用" readonly></div>
        </div>
        <button type="button" class="btn-primary" style="width:100%" id="pf-save">确认提取（联动现金）</button>
      `);
      document.getElementById('pf-save').onclick = () => {
        const wd = Number(document.getElementById('pf-wd').value);
        if (wd > 0 && wd <= state.assets.provident) {
          state.assets.provident -= wd;
          state.assets.cash += wd;
          state.cashMoves.unshift({ date: '2026-11-19', type: 'flow', title: '公积金提取到账', amount: wd, note: '公积金 → 现金（总资产不变）' });
          closeSheet();
          renderProvident();
          toast(`已提取 ¥${fmt(wd)} 至现金，总资产保持不变`);
        }
      };
    };
  }

  function renderGold() {
    const val = goldVal();
    const holdProfit = val - state.assets.goldCost;
    const totalProfit = holdProfit + state.assets.goldRealized;
    const avgCost = state.assets.goldCost / state.assets.goldGrams;
    const totalPct = (totalProfit / state.assets.goldCost) * 100;

    document.getElementById('screen-gold').innerHTML = `
      <section class="hero">
        <div class="hero-top">
          <button type="button" class="hero-icon-btn" id="gold-back"><svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></button>
          <span class="hero-label" style="margin:0;font-size:16px">贵金属</span>
          <button type="button" class="hero-icon-btn" id="gold-sync" style="width:auto;padding:0 12px;border-radius:99px;font-size:12px;font-weight:700">刷新报价</button>
        </div>
        <div class="hero-amount tabular" style="margin-top:14px;font-size:36px">${mask('¥' + fmt(val))}</div>
        <div class="hero-subnote">Au99.99 现货参考价 · ¥${fmt(state.assets.goldPrice)}/克 · 持有 ${state.assets.goldGrams.toFixed(4)} 克</div>
        <div class="hero-two-col" style="grid-template-columns:repeat(3,1fr)">
          <div class="hero-metric"><small>总收益</small><b class="tabular" style="font-size:15px;color:var(--color-finance-gain-on-hero)">${mask(signed(totalProfit))}</b></div>
          <div class="hero-metric"><small>总收益率</small><b class="tabular" style="font-size:15px;color:var(--color-finance-gain-on-hero)">+${totalPct.toFixed(1)}%</b></div>
          <div class="hero-metric"><small>成本均价</small><b class="tabular" style="font-size:15px">¥${fmt(avgCost)}</b></div>
        </div>
      </section>
      <div class="page-body">
        <section class="card">
          <div class="section-head"><h2>收益拆解与说明</h2></div>
          <div style="display:flex;justify-content:space-between;font-size:13px"><span>持有收益</span><b class="gain tabular">${mask(signed(holdProfit))}</b></div>
          <div style="display:flex;justify-content:space-between;font-size:13px;margin-top:8px"><span>已实现收益</span><b class="gain tabular">${mask(signed(state.assets.goldRealized))}</b></div>
          <div style="margin-top:10px;font-size:12px;color:var(--color-text-secondary)">支持自动三级参考报价（上金所 Au99.99 现货 → 上金所延时 → 工行积存金），卖出默认直接到账至现金。</div>
        </section>
      </div>
    `;
    document.getElementById('gold-back').onclick = () => go('home');
    document.getElementById('gold-sync').onclick = () => {
      state.assets.goldPrice = state.assets.goldPrice === 900 ? 906.5 : 900;
      renderGold();
      toast(`已刷新 Au99.99 现货参考价：¥${fmt(state.assets.goldPrice)}/克`);
    };
  }

  // 全局事件绑定
  document.querySelectorAll('#app-nav [data-nav]').forEach(b => b.onclick = () => go(b.dataset.nav));
  document.getElementById('nav-entry').onclick = () => go('entry');
  document.getElementById('sheet-close').onclick = closeSheet;
  document.getElementById('sheet-backdrop').onclick = (e) => { if (e.target.id === 'sheet-backdrop') closeSheet(); };
  document.querySelectorAll('[data-theme-pref]').forEach(b => b.onclick = () => applyTheme(b.dataset.themePref));
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeSheet(); });

  applyTheme('light');
  render();
})();
