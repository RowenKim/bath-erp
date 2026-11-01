-- =============================================
-- BathERP 데모 : 샘플 데이터
-- =============================================
USE BathErp;
GO

INSERT INTO dbo.ITEM (item_code, item_name, unit_price, can_produce, safety_stock) VALUES
('TC-100', N'양변기',          180000, 1, 10),
('LV-200', N'세면대',           95000, 1, 10),
('FC-300', N'수전(단종)',        48000, 0,  0);   -- 단종: 재고 부족 시 생산 불가 → 실패·롤백 시연용

INSERT INTO dbo.CUSTOMER (cust_code, cust_name) VALUES
('C001', N'서울대리점'),
('C002', N'부산대리점');

INSERT INTO dbo.ACCOUNT (account_code, account_name) VALUES
('1108', N'외상매출금'),
('4101', N'제품매출');

-- 기초 재고 + 같은 수량을 수불에도 '기초'로 기록 (재고 = 수불 합계가 처음부터 맞도록)
INSERT INTO dbo.STOCK (item_code, qty) VALUES
('TC-100', 20), ('LV-200', 30), ('FC-300', 5);

INSERT INTO dbo.STOCK_TXN (item_code, txn_type, qty, ref_no) VALUES
('TC-100', N'기초', 20, NULL),
('LV-200', N'기초', 30, NULL),
('FC-300', N'기초',  5, NULL);
GO

-- 정합성 확인 쿼리 (면접 때 보여주기 좋음): 결과가 0건이면 재고와 수불이 일치
-- SELECT s.item_code, s.qty AS stock_qty, t.sum_qty
-- FROM dbo.STOCK s
-- JOIN (SELECT item_code, SUM(qty) AS sum_qty FROM dbo.STOCK_TXN GROUP BY item_code) t
--   ON t.item_code = s.item_code
-- WHERE s.qty <> t.sum_qty;
