-- =============================================
-- BathERP 데모 : 화면 조회용 프로시저 (완성본 · 참고용)
-- 결과셋 4개를 한 번에 돌려줌 → C#에서 NextResult()로 차례로 읽음
-- =============================================
USE BathErp;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Get
AS
BEGIN
    SET NOCOUNT ON;

    -- 1) 재고 (+ 수불 합계를 같이 보여줘서 정합성 확인)
    SELECT i.item_code, i.item_name, i.unit_price, i.can_produce,
           s.qty,
           ISNULL(t.sum_qty, 0) AS txn_sum
    FROM dbo.ITEM i
    JOIN dbo.STOCK s ON s.item_code = i.item_code
    LEFT JOIN (SELECT item_code, SUM(qty) AS sum_qty
               FROM dbo.STOCK_TXN GROUP BY item_code) t
           ON t.item_code = i.item_code
    ORDER BY i.item_code;

    -- 2) 최근 주문 50건
    SELECT TOP 50 m.order_no, c.cust_name, d.item_code, d.qty, m.total_amount, m.status, m.created_at
    FROM dbo.ORDER_MST m
    JOIN dbo.ORDER_DTL d ON d.order_no = m.order_no AND d.line_no = 1
    JOIN dbo.CUSTOMER c  ON c.cust_code = m.cust_code
    ORDER BY m.created_at DESC;

    -- 3) 최근 전표 분개 100줄 (전표 1건당 2줄)
    SELECT TOP 100 v.voucher_no, v.order_no, a.account_name, d.debit, d.credit
    FROM dbo.VOUCHER_MST v
    JOIN dbo.VOUCHER_DTL d ON d.voucher_no = v.voucher_no
    JOIN dbo.ACCOUNT a     ON a.account_code = d.account_code
    ORDER BY v.voucher_no DESC, d.line_no;

    -- 4) 최근 로그 50건
    SELECT TOP 50 log_id, ref_no, action, result, message, created_at
    FROM dbo.IF_LOG
    ORDER BY log_id DESC;
END
GO
