// 주문 → (재고 부족이면) 생산 연출 5초 → 생산 완료 API → 주문 재요청 → 결과 표시
const $ = (s) => document.querySelector(s);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const PRODUCTION_MS = 5000;   // 화면 연출 시간. DB는 기다리지 않음(트랜잭션 안에서 대기하면 락이 길어짐)

async function post(url, body) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(data.error || `요청 실패 (${res.status})`);
  return data;
}

async function runProduction(itemCode, shortage) {
  const ov = $('#overlay'), bar = $('#ovBar');
  $('#ovTitle').textContent = '생산팀 작업 중';
  $('#ovText').textContent = `재고가 ${shortage}개 부족해 생산을 요청했어요. 부족분과 안전재고만큼 생산합니다.`;
  bar.style.transition = 'none';
  bar.style.width = '0';
  ov.hidden = false;
  bar.getBoundingClientRect();                       // 너비 0을 먼저 반영시킨 뒤 애니메이션 시작
  bar.style.transition = `width ${PRODUCTION_MS}ms linear`;
  bar.style.width = '100%';

  await sleep(PRODUCTION_MS);
  const p = await post('/api/production', { itemCode, shortage });

  $('#ovTitle').textContent = '생산 완료';
  $('#ovText').textContent = `${p.prodNo} · ${p.producedQty}개 입고. 주문을 이어서 처리합니다.`;
  await sleep(900);
  ov.hidden = true;
}

function finish(msg, kind) {
  // 새로고침해도 결과 문구가 보이도록 잠깐 저장했다가 표시
  sessionStorage.setItem('flash', JSON.stringify({ msg, kind }));
  location.reload();
}

$('#orderBtn').addEventListener('click', async (e) => {
  const req = {
    custCode: $('#cust').value,
    itemCode: $('#item').value,
    qty: Number($('#qty').value),
  };
  e.target.disabled = true;
  try {
    let r = await post('/api/orders', req);

    if (r.result === 'NEED_PRODUCTION') {
      await runProduction(req.itemCode, r.shortage);
      r = await post('/api/orders', req);            // 생산 입고 후 같은 주문 재요청
    }

    if (r.result === 'OK') finish(`주문 ${r.orderNo} 처리 완료: 재고 출고, 매출 전표 생성`, 'ok');
    else if (r.result === 'TODO') finish('usp_Order_Process가 아직 TODO 상태예요. 03_procedures.sql을 채워주세요.', 'err');
    else finish(`예상하지 못한 결과: ${r.result}`, 'err');
  } catch (err) {
    $('#overlay').hidden = true;
    finish(`주문 실패: ${err.message}`, 'err');
  }
});

// 새로고침 직후 결과 문구 표시
(() => {
  const raw = sessionStorage.getItem('flash');
  if (!raw) return;
  sessionStorage.removeItem('flash');
  const { msg, kind } = JSON.parse(raw);
  const b = $('#banner');
  b.textContent = msg;
  b.className = `banner ${kind}`;
  b.hidden = false;
})();
