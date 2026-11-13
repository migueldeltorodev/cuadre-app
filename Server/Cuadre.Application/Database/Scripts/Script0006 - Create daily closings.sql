-- Slice 6: the daily closing (RF-CIE-01..08, 10, 12, 14), staff pay (RF-PER-01/02) and the internal debt of
-- Reposición (RF-PF-09, RN-10). A closing is a document like a purchase: it writes its sales into the stock ledger
-- and its collections, split and staff pay into the cash ledger, all in one transaction.
-- It is never edited. "Reopening" a day = voiding its closing (status 'Voided' + reason): the reversal movements are
-- dated THE SAME DAY, so the day ends exactly as if it had never been closed and can be closed again.
-- Units: money in CUP cents, quantities in thousandths of the stock unit, unit costs in millionths.

-- Who worked (RF-CIE-14). Only for the history and the claims: the pay belongs to the post, not the person.
CREATE TABLE employees (
    id          TEXT    NOT NULL PRIMARY KEY,
    name        TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    is_active   INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at  TEXT    NOT NULL,
    updated_at  TEXT    NOT NULL
) STRICT;

-- Pay of the post: fixed per day + % of A1, from a date on (RN-08, RF-PER-01/02). Like the allocation plans:
-- a change is a new scheme, so past closings keep what they paid. Seeded "since always": 1,000 CUP + 1%.
CREATE TABLE staff_pay_schemes (
    id                    TEXT    NOT NULL PRIMARY KEY,
    effective_from        TEXT    NOT NULL UNIQUE
                                  CHECK (effective_from GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    daily_fixed_cents     INTEGER NOT NULL CHECK (daily_fixed_cents >= 0),
    percent_of_sales_bps  INTEGER NOT NULL CHECK (percent_of_sales_bps BETWEEN 0 AND 10000),
    notes                 TEXT,
    created_at            TEXT    NOT NULL,
    updated_at            TEXT    NOT NULL
) STRICT;

-- One confirmed closing per day. The figures are a SNAPSHOT of what was computed and moved that day.
CREATE TABLE daily_closings (
    id                         TEXT    NOT NULL PRIMARY KEY,
    date                       TEXT    NOT NULL CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    status                     TEXT    NOT NULL CHECK (status IN ('Confirmed', 'Voided')),
    employee_id                TEXT    REFERENCES employees (id),
    notes                      TEXT,
    -- Σ sold × price (RN-06) and the shrinkage it accepts (bulk weighing).
    theoretical_sales_cents    INTEGER NOT NULL,
    shrinkage_tolerance_cents  INTEGER NOT NULL CHECK (shrinkage_tolerance_cents >= 0),
    -- A1 = cash + transfers (D-02: what was really collected).
    cash_collected_cents       INTEGER NOT NULL CHECK (cash_collected_cents >= 0),
    transfer_collected_cents   INTEGER NOT NULL CHECK (transfer_collected_cents >= 0),
    -- A2 = Σ sold × historical CUP cost (RN-07). A3 = A1 − A2 is derived.
    cost_of_sales_cents        INTEGER NOT NULL,
    -- A1 − theoretical; and after the tolerance (what is recorded as shortage/surplus).
    cash_difference_cents      INTEGER NOT NULL,
    adjusted_difference_cents  INTEGER NOT NULL,
    threshold_cents            INTEGER NOT NULL CHECK (threshold_cents >= 0),
    -- RN-08 with the scheme in force that day.
    staff_pay_cents            INTEGER NOT NULL CHECK (staff_pay_cents >= 0),
    pay_fixed_cents            INTEGER NOT NULL CHECK (pay_fixed_cents >= 0),
    pay_percent_bps            INTEGER NOT NULL CHECK (pay_percent_bps BETWEEN 0 AND 10000),
    -- RN-10.4: what Reposición owes the other envelopes, before and after this day.
    debt_before_cents          INTEGER NOT NULL CHECK (debt_before_cents >= 0),
    debt_created_cents         INTEGER NOT NULL CHECK (debt_created_cents >= 0),
    debt_repaid_cents          INTEGER NOT NULL CHECK (debt_repaid_cents >= 0),
    debt_after_cents           INTEGER NOT NULL CHECK (debt_after_cents >= 0),
    void_reason                TEXT,
    voided_at                  TEXT,
    created_at                 TEXT    NOT NULL,
    updated_at                 TEXT    NOT NULL,
    CHECK ((status = 'Voided') = (void_reason IS NOT NULL)),
    CHECK (debt_after_cents = debt_before_cents - debt_repaid_cents + debt_created_cents)
) STRICT;

CREATE UNIQUE INDEX ux_daily_closings_date ON daily_closings (date) WHERE status = 'Confirmed';
CREATE INDEX ix_daily_closings_date ON daily_closings (date);

-- The counter count of the day (RN-05): one line per (product, counter location). Snapshot of the unit too.
CREATE TABLE daily_closing_lines (
    id                    TEXT    NOT NULL PRIMARY KEY,
    closing_id            TEXT    NOT NULL REFERENCES daily_closings (id),
    line_no               INTEGER NOT NULL,
    product_id            TEXT    NOT NULL REFERENCES products (id),
    location_id           TEXT    NOT NULL REFERENCES locations (id),
    stock_unit            TEXT    NOT NULL,
    opening_milli         INTEGER NOT NULL,
    in_milli              INTEGER NOT NULL CHECK (in_milli >= 0),
    out_milli             INTEGER NOT NULL CHECK (out_milli >= 0),
    expected_milli        INTEGER NOT NULL,
    counted_milli         INTEGER NOT NULL CHECK (counted_milli >= 0),
    -- expected − counted. Negative = more than there should be (written as an entry, without cost).
    sold_milli            INTEGER NOT NULL,
    unit_price_cents      INTEGER,
    sales_cents           INTEGER NOT NULL,
    tolerance_bps         INTEGER NOT NULL CHECK (tolerance_bps BETWEEN 0 AND 10000),
    tolerance_cents       INTEGER NOT NULL CHECK (tolerance_cents >= 0),
    unit_cost_usd_micros  INTEGER,
    unit_cost_cup_micros  INTEGER,
    cost_cents            INTEGER NOT NULL,
    UNIQUE (closing_id, line_no),
    UNIQUE (closing_id, product_id, location_id)
) STRICT;

CREATE INDEX ix_daily_closing_lines_product_id ON daily_closing_lines (product_id);

-- What was collected with each payment method (RF-CAJ-02). Transfer = it landed in a Bank box.
CREATE TABLE daily_closing_collections (
    closing_id         TEXT    NOT NULL REFERENCES daily_closings (id),
    payment_method_id  TEXT    NOT NULL REFERENCES payment_methods (id),
    cash_box_id        TEXT    NOT NULL REFERENCES cash_boxes (id),
    is_transfer        INTEGER NOT NULL CHECK (is_transfer IN (0, 1)),
    amount_cents       INTEGER NOT NULL CHECK (amount_cents >= 0),
    PRIMARY KEY (closing_id, payment_method_id)
) STRICT, WITHOUT ROWID;

-- The split of the day per envelope (RN-09/RN-10): what it received in total, and how much of it is cash to put
-- physically in the envelope and how much is already in the bank. Reposición has no %.
CREATE TABLE daily_closing_allocations (
    closing_id    TEXT    NOT NULL REFERENCES daily_closings (id),
    envelope_id   TEXT    NOT NULL REFERENCES envelopes (id),
    percent_bps   INTEGER CHECK (percent_bps IS NULL OR percent_bps BETWEEN 0 AND 10000),
    amount_cents  INTEGER NOT NULL,
    cash_cents    INTEGER NOT NULL,
    bank_cents    INTEGER NOT NULL,
    PRIMARY KEY (closing_id, envelope_id)
) STRICT, WITHOUT ROWID;

-- Shortages and surpluses of the cash count (RN-06c, RF-CIE-12). Followed up until resolved (slice 6b):
-- Pending → Recovered / Lost. Surpluses are only Recorded. Voided when their closing is reopened.
CREATE TABLE cash_discrepancies (
    id            TEXT    NOT NULL PRIMARY KEY,
    closing_id    TEXT    NOT NULL REFERENCES daily_closings (id),
    date          TEXT    NOT NULL CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    -- Shortage, Surplus. Status: Pending, Recorded, Voided (+ the slice 6b ones). No CHECK on purpose.
    kind          TEXT    NOT NULL,
    amount_cents  INTEGER NOT NULL CHECK (amount_cents > 0),
    employee_id   TEXT    REFERENCES employees (id),
    note          TEXT,
    status        TEXT    NOT NULL,
    created_at    TEXT    NOT NULL,
    updated_at    TEXT    NOT NULL
) STRICT;

CREATE INDEX ix_cash_discrepancies_closing_id ON cash_discrepancies (closing_id);
CREATE INDEX ix_cash_discrepancies_status ON cash_discrepancies (status, date);

-- Both ledgers learn the closing behind their new rows (sales, collections, split, staff pay).
ALTER TABLE stock_movements ADD COLUMN closing_id TEXT REFERENCES daily_closings (id);
ALTER TABLE cash_movements ADD COLUMN closing_id TEXT REFERENCES daily_closings (id);

CREATE INDEX ix_stock_movements_closing ON stock_movements (closing_id);
CREATE INDEX ix_cash_movements_closing ON cash_movements (closing_id);
CREATE INDEX ix_stock_movements_location_date ON stock_movements (location_id, date);
