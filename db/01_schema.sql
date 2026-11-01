-- =============================================
-- BathERP 데모 : 테이블 · 시퀀스 생성
-- 실행 순서: 01_schema → 02_seed → 03_procedures(직접 작성) → 04_dashboard
-- =============================================
IF DB_ID('BathErp') IS NULL
    CREATE DATABASE BathErp;
GO
USE BathErp;
GO

-- ---------- 번호 채번용 시퀀스 ----------
CREATE SEQUENCE dbo.seq_order   AS INT START WITH 1 INCREMENT BY 1;   -- SO-0001
CREATE SEQUENCE dbo.seq_voucher AS INT START WITH 1 INCREMENT BY 1;   -- VC-0001
CREATE SEQUENCE dbo.seq_prod    AS INT START WITH 1 INCREMENT BY 1;   -- PR-0001
GO

-- ---------- 마스터 ----------
CREATE TABLE dbo.ITEM (
    item_code     VARCHAR(20)    NOT NULL PRIMARY KEY,
    item_name     NVARCHAR(50)   NOT NULL,
    unit_price    DECIMAL(18,0)  NOT NULL,
    can_produce   BIT            NOT NULL DEFAULT 1,   -- 0 = 단종(생산 불가)
    safety_stock  INT            NOT NULL DEFAULT 0    -- 생산 시 부족분에 더해 만드는 수량
);

CREATE TABLE dbo.CUSTOMER (
    cust_code     VARCHAR(20)    NOT NULL PRIMARY KEY,
    cust_name     NVARCHAR(50)   NOT NULL
);

CREATE TABLE dbo.ACCOUNT (
    account_code  VARCHAR(10)    NOT NULL PRIMARY KEY,
    account_name  NVARCHAR(50)   NOT NULL
);
GO

-- ---------- 재고 (MM) ----------
CREATE TABLE dbo.STOCK (
    item_code     VARCHAR(20)    NOT NULL PRIMARY KEY
                  REFERENCES dbo.ITEM(item_code),
    qty           INT            NOT NULL CHECK (qty >= 0),
    updated_at    DATETIME2      NOT NULL DEFAULT SYSDATETIME()
);

-- 재고 수불: 재고를 바꾼 사건을 한 건씩 기록 (재고 = 수불 합계)
CREATE TABLE dbo.STOCK_TXN (
    txn_id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    item_code     VARCHAR(20)    NOT NULL REFERENCES dbo.ITEM(item_code),
    txn_type      NVARCHAR(10)   NOT NULL,              -- 기초 / 출고 / 생산입고
    qty           INT            NOT NULL,              -- 입고 +, 출고 -
    ref_no        VARCHAR(20)    NULL,                  -- 근거 번호 (SO-0001, PR-0001)
    created_at    DATETIME2      NOT NULL DEFAULT SYSDATETIME()
);
CREATE INDEX IX_STOCK_TXN_item ON dbo.STOCK_TXN(item_code, created_at);
GO

-- ---------- 주문 (SD) ----------
CREATE TABLE dbo.ORDER_MST (
    order_no      VARCHAR(20)    NOT NULL PRIMARY KEY,
    cust_code     VARCHAR(20)    NOT NULL REFERENCES dbo.CUSTOMER(cust_code),
    order_date    DATE           NOT NULL,
    status        NVARCHAR(10)   NOT NULL,              -- 출고완료
    total_amount  DECIMAL(18,0)  NOT NULL,
    created_at    DATETIME2      NOT NULL DEFAULT SYSDATETIME()
);

CREATE TABLE dbo.ORDER_DTL (
    order_no      VARCHAR(20)    NOT NULL REFERENCES dbo.ORDER_MST(order_no),  -- 어느 주문의 상세인지
    line_no       INT            NOT NULL,              -- 그 주문 안에서 몇 번째 줄인지 (1, 2, 3 ...). 이번 데모는 품목 1개라 항상 1
    item_code     VARCHAR(20)    NOT NULL REFERENCES dbo.ITEM(item_code),
    qty           INT            NOT NULL CHECK (qty > 0),
    unit_price    DECIMAL(18,0)  NOT NULL,
    amount        DECIMAL(18,0)  NOT NULL,
    CONSTRAINT PK_ORDER_DTL PRIMARY KEY (order_no, line_no)   -- 주문번호 + 줄번호 조합이 한 줄을 유일하게 구분
);
GO

-- ---------- 전표 (FI) ----------
CREATE TABLE dbo.VOUCHER_MST (
    voucher_no    VARCHAR(20)    NOT NULL PRIMARY KEY,
    voucher_date  DATE           NOT NULL,
    order_no      VARCHAR(20)    NULL REFERENCES dbo.ORDER_MST(order_no),
    description   NVARCHAR(200)  NULL
);

CREATE TABLE dbo.VOUCHER_DTL (
    voucher_no    VARCHAR(20)    NOT NULL REFERENCES dbo.VOUCHER_MST(voucher_no),  -- 어느 전표의 분개인지
    line_no       INT            NOT NULL,              -- 전표 안의 줄번호. 1 = 차변(외상매출금), 2 = 대변(제품매출)
    account_code  VARCHAR(10)    NOT NULL REFERENCES dbo.ACCOUNT(account_code),
    debit         DECIMAL(18,0)  NOT NULL DEFAULT 0,    -- 차변
    credit        DECIMAL(18,0)  NOT NULL DEFAULT 0,    -- 대변
    CONSTRAINT PK_VOUCHER_DTL PRIMARY KEY (voucher_no, line_no)   -- 전표번호 + 줄번호 조합이 기본키
);
GO

-- ---------- 인터페이스 로그 ----------
-- 일부러 FK 없음: 실패한 주문은 롤백돼 ORDER_MST에 남지 않으므로 FK를 걸면 실패 로그를 쓸 수 없음
CREATE TABLE dbo.IF_LOG (
    log_id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    ref_no        VARCHAR(20)    NULL,
    action        NVARCHAR(10)   NOT NULL,              -- 주문 / 생산
    result        NVARCHAR(10)   NOT NULL,              -- 성공 / 실패 / 생산필요
    message       NVARCHAR(400)  NULL,
    created_at    DATETIME2      NOT NULL DEFAULT SYSDATETIME()
);
GO
