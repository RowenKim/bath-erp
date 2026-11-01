-- =============================================
-- BathERP 데모 : 주문 처리 · 생산 완료 저장 프로시저 (주문 프로시저는 직접 작성)
-- 아래 "약속(입력·출력)"은 C# 코드가 이 이름과 타입 그대로 호출하므로 바꾸지 마세요.
-- =============================================
USE BathErp;
GO

/* ------------------------------------------------------------------
   1) usp_Order_Process : 주문 1건 처리 (SD → MM → FI)

   [약속]
   입력  @CustCode, @ItemCode, @Qty
   출력  @Result   'OK'              : 출고·전표까지 완료 (COMMIT)
                   'NEED_PRODUCTION' : 재고 부족 + 생산 가능 → 아무것도 바꾸지 않고 종료
         @OrderNo  'SO-0001' 형식 (OK일 때만)
         @Shortage 부족 수량 (NEED_PRODUCTION일 때만)
   오류  THROW 50001  재고 부족 + 단종 품목(생산 불가)
         THROW 50002  입력 오류 (수량 0 이하, 없는 품목·거래처)

   [재고 충분일 때 한 트랜잭션 안에서 할 일]
   1. ORDER_MST 1건, ORDER_DTL 1줄 등록 (상태 '출고완료', 금액 = 단가 × 수량)
   2. STOCK 차감 + STOCK_TXN에 '출고' -수량, ref_no = 주문번호
   3. VOUCHER_MST 1건 + VOUCHER_DTL 2줄
        1줄: 1108 외상매출금  차변 = 금액
        2줄: 4101 제품매출    대변 = 금액
   4. IF_LOG 에 '주문' / '성공'

   [실패 시]
   CATCH 에서 ROLLBACK 을 "먼저" 하고, 그다음 IF_LOG 에 '실패' 기록, 마지막에 THROW; 로 다시 던지기
   (순서가 바뀌면 로그까지 롤백돼서 사라짐)

   [힌트]
   - 재고 읽을 때:  SELECT @Stock = qty FROM dbo.STOCK WITH (UPDLOCK, ROWLOCK) WHERE item_code = @ItemCode;
     UPDLOCK 없이 읽으면 동시에 들어온 두 주문이 둘 다 "재고 충분"으로 읽을 수 있음
   - 번호 채번:     SET @OrderNo = 'SO-' + RIGHT('0000' + CAST(NEXT VALUE FOR dbo.seq_order AS VARCHAR(10)), 4);
                    전표는 dbo.seq_voucher, 'VC-' 접두어
   - 업무 오류:     THROW 50001, N'단종 품목이라 생산할 수 없어 주문을 처리할 수 없습니다.', 1;
                    (THROW 앞 문장은 세미콜론으로 끝나야 함)
------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_Order_Process
    @CustCode  VARCHAR(20),
    @ItemCode  VARCHAR(20),
    @Qty       INT,
    @Result    VARCHAR(20) OUTPUT,
    @OrderNo   VARCHAR(20) OUTPUT,
    @Shortage  INT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;   -- 예상 못 한 오류에도 트랜잭션이 확실히 롤백되게

    SET @Result = NULL; SET @OrderNo = NULL; SET @Shortage = 0;

    DECLARE @Stock INT, @CanProduce BIT, @UnitPrice DECIMAL(18,0), @Amount DECIMAL(18,0), @VoucherNo VARCHAR(20);

    BEGIN TRY
        -- 0. 입력 검증
        IF ISNULL(@CustCode, '') = '' OR ISNULL(@ItemCode, '') = '' OR ISNULL(@Qty, 0) <= 0
        BEGIN
            THROW 50002, N'거래처, 품목, 수량을 확인해주세요.', 1;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.ITEM WHERE item_code = @ItemCode)
        BEGIN
            THROW 50002, N'존재하지 않는 품목입니다.', 1;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.CUSTOMER WHERE cust_code = @CustCode)
        BEGIN
            THROW 50002, N'존재하지 않는 거래처입니다.', 1;
        END

        BEGIN TRAN;

        -- 1. 재고·단가·생산가능 여부 읽기 (UPDLOCK)
        SELECT @Stock = s.qty, @CanProduce = i.can_produce, @UnitPrice = i.unit_price
        FROM dbo.STOCK s WITH (UPDLOCK, ROWLOCK)
            JOIN dbo.ITEM i ON i.item_code = s.item_code
        WHERE s.item_code = @ItemCode;

        -- 2. 재고 부족이면: 단종은 실패, 생산 가능은 NEED_PRODUCTION 반환 후 종료
        IF @Stock < @Qty
        BEGIN
            IF @CanProduce < 1
            BEGIN
                THROW 50001, N'단종 품목이라 생산할 수 없어 주문을 처리할 수 없습니다.', 1;
            END
            ELSE
            BEGIN
                SET @Result = 'NEED_PRODUCTION';
                SET @Shortage = @Qty - @Stock;

                INSERT INTO dbo.IF_LOG (ref_no, action, result, message)
                VALUES (NULL, N'주문', N'생산필요',
                        @ItemCode + N' 재고 부족 ' + CAST(@Shortage AS NVARCHAR(10)) + N'개');
                COMMIT;
                RETURN;
            END
        END

        -- 3. 주문 MST·DTL 등록
        SET @OrderNo = 'SO-' + RIGHT('0000' + CAST(NEXT VALUE FOR dbo.seq_order AS VARCHAR(10)), 4);
        SET @Amount = @UnitPrice * @Qty;

        INSERT INTO dbo.ORDER_MST (order_no, cust_code, order_date, status, total_amount, created_at)
        VALUES (@OrderNo, @CustCode, GETDATE(), N'출고완료', @Amount, SYSDATETIME());

        INSERT INTO dbo.ORDER_DTL (order_no, line_no, item_code, qty, unit_price, amount)
        VALUES (@OrderNo, 1, @ItemCode, @Qty, @UnitPrice, @Amount);

        -- 4. 재고 차감 + 수불 '출고'
        UPDATE dbo.STOCK
        SET qty = qty - @Qty,
            updated_at = SYSDATETIME()
        WHERE item_code = @ItemCode;

        INSERT INTO dbo.STOCK_TXN (item_code, txn_type, qty, ref_no)
        VALUES (@ItemCode, N'출고', -@Qty, @OrderNo);

        -- 5. 전표 MST + DTL 2줄 (차변 1108 외상매출금 / 대변 4101 제품매출)
        SET @VoucherNo = 'VC-' + RIGHT('0000' + CAST(NEXT VALUE FOR dbo.seq_voucher AS VARCHAR(10)), 4);

        INSERT INTO dbo.VOUCHER_MST (voucher_no, voucher_date, order_no, description)
        VALUES (@VoucherNo, CAST(SYSDATETIME() AS DATE), @OrderNo, N'제품 매출 ' + @OrderNo);

        INSERT INTO dbo.VOUCHER_DTL (voucher_no, line_no, account_code, debit, credit)
        VALUES (@VoucherNo, 1, '1108', @Amount, 0),
               (@VoucherNo, 2, '4101', 0,       @Amount);

        -- 6. 성공 로그
        INSERT INTO dbo.IF_LOG (ref_no, action, result, message)
        VALUES (@OrderNo, N'주문', N'성공', N'출고·전표 생성 완료');

        SET @Result = 'OK';
        COMMIT;
    END TRY
    BEGIN CATCH
        -- 7. 롤백 → 실패 로그 → 다시 던지기 (순서가 바뀌면 로그까지 롤백됨)
        IF @@TRANCOUNT > 0 ROLLBACK;

        INSERT INTO dbo.IF_LOG (ref_no, action, result, message)
        VALUES (@OrderNo, N'주문', N'실패',
                LEFT(ISNULL(@ItemCode, N'') + N' ' + ERROR_MESSAGE(), 400));

        THROW;
    END CATCH
END
GO


/* ------------------------------------------------------------------
   2) usp_Production_Complete : 생산 완료(시뮬레이션) → 재고 입고

   [약속]
   입력  @ItemCode, @Shortage(부족 수량)
   출력  @ProdNo      'PR-0001' 형식
         @ProducedQty 생산 수량 = 부족 수량 + ITEM.safety_stock
   오류  THROW 50003  단종 품목(can_produce = 0)

   [한 트랜잭션 안에서 할 일]
   1. 생산 수량 계산
   2. STOCK 증가 + STOCK_TXN '생산입고' +수량, ref_no = 생산번호
   3. IF_LOG '생산' / '성공'

   위 주문 프로시저를 먼저 완성하면, 이건 같은 패턴이라 금방 써져요.
------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_Production_Complete
    @ItemCode     VARCHAR(20),
    @Shortage     INT,
    @ProdNo       VARCHAR(20) OUTPUT,
    @ProducedQty  INT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @ProdNo = NULL; SET @ProducedQty = 0;

    DECLARE @CanProduce BIT, @Safety INT;

    BEGIN TRY
        -- 입력 검증 (주문 프로시저와 같은 방식)
        IF ISNULL(@ItemCode, '') = '' OR ISNULL(@Shortage, 0) <= 0
        BEGIN
            THROW 50002, N'품목과 부족 수량을 확인해주세요.', 1;
        END

        BEGIN TRAN;

        -- 1. 품목 정보 읽기 → 생산 가능 여부 확인 → 생산 수량·번호 계산
        SELECT @CanProduce = can_produce, @Safety = safety_stock
        FROM dbo.ITEM
        WHERE item_code = @ItemCode;

        IF @CanProduce IS NULL          -- 읽어온 줄이 없으면 변수가 NULL 그대로 = 없는 품목
        BEGIN
            THROW 50002, N'존재하지 않는 품목입니다.', 1;
        END

        IF @CanProduce = 0              -- 단종
        BEGIN
            THROW 50003, N'단종 품목이라 생산할 수 없습니다.', 1;
        END

        SET @ProducedQty = @Shortage + @Safety;     -- 부족분 + 안전재고
        SET @ProdNo = 'PR-' + RIGHT('0000' + CAST(NEXT VALUE FOR dbo.seq_prod AS VARCHAR(10)), 4);

        -- 2. 재고 증가 + 수불 '생산입고' (입고라서 플러스)
        UPDATE dbo.STOCK
        SET qty = qty + @ProducedQty,
            updated_at = SYSDATETIME()
        WHERE item_code = @ItemCode;

        INSERT INTO dbo.STOCK_TXN (item_code, txn_type, qty, ref_no)
        VALUES (@ItemCode, N'생산입고', @ProducedQty, @ProdNo);

        -- 3. 성공 로그
        INSERT INTO dbo.IF_LOG (ref_no, action, result, message)
        VALUES (@ProdNo, N'생산', N'성공',
                @ItemCode + N' ' + CAST(@ProducedQty AS NVARCHAR(10)) + N'개 생산 입고 (부족 '
                + CAST(@Shortage AS NVARCHAR(10)) + N' + 안전재고 ' + CAST(@Safety AS NVARCHAR(10)) + N')');

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;     -- 먼저 롤백

        INSERT INTO dbo.IF_LOG (ref_no, action, result, message)   -- 그다음 실패 로그
        VALUES (@ProdNo, N'생산', N'실패',
                LEFT(ISNULL(@ItemCode, N'') + N' ' + ERROR_MESSAGE(), 400));

        THROW;                           -- 마지막에 다시 던지기
    END CATCH
END
GO
