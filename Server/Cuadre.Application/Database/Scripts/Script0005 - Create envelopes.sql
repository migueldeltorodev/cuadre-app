-- Slice 5: Profit First envelopes (RF-PF-01..08, RF-PF-11, RF-DUE-01/02, RF-IMP-01).
-- A cash box says WHERE the money is; an envelope says what it is FOR (D-10). Every row of the cash ledger now
-- belongs to exactly one envelope, so per cash box: SUM of its envelopes = the box balance. Never stored, always
-- summed from cash_movements.
-- Default envelopes and the first allocation plan are inserted by DatabaseSeeder (like the cash boxes), which
-- also assigns the cash movements written before this slice (purchases) to Reposición.

CREATE TABLE envelopes (
    id          TEXT    NOT NULL PRIMARY KEY,
    name        TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    -- Profit, OwnerPay, Tax, Operations, Replenishment: one each, the app's rules point at them.
    -- Custom: created by the user. No CHECK on purpose: the C# enum is the source of truth.
    role        TEXT    NOT NULL,
    sort_order  INTEGER NOT NULL,
    is_active   INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at  TEXT    NOT NULL,
    updated_at  TEXT    NOT NULL
) STRICT;

CREATE UNIQUE INDEX ux_envelopes_role ON envelopes (role) WHERE role <> 'Custom';

-- The % of A3 that goes to each envelope, from a date on (RF-PF-05). Never edited once a later plan exists:
-- a change is a new plan, so past splits keep the % they were made with. Reposición never has a %.
CREATE TABLE allocation_plans (
    id              TEXT    NOT NULL PRIMARY KEY,
    effective_from  TEXT    NOT NULL UNIQUE
                            CHECK (effective_from GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    notes           TEXT,
    created_at      TEXT    NOT NULL,
    updated_at      TEXT    NOT NULL
) STRICT;

CREATE TABLE allocation_plan_items (
    plan_id      TEXT    NOT NULL REFERENCES allocation_plans (id),
    envelope_id  TEXT    NOT NULL REFERENCES envelopes (id),
    -- Basis points: 30% = 3000. The items of a plan add up to 10000 (checked by the service).
    percent_bps  INTEGER NOT NULL CHECK (percent_bps BETWEEN 0 AND 10000),
    PRIMARY KEY (plan_id, envelope_id)
) STRICT, WITHOUT ROWID;

-- Money documents of the envelopes: opening balances, moves between envelopes, withdrawals and tax payments.
-- Like stock operations: never edited, voided with a reason (reversal movements dated the day of the void).
CREATE TABLE envelope_operations (
    id                TEXT    NOT NULL PRIMARY KEY,
    number            INTEGER NOT NULL UNIQUE CHECK (number > 0),
    -- Opening, Transfer, Withdrawal, TaxPayment. No CHECK on purpose (see Script0003).
    kind              TEXT    NOT NULL,
    date              TEXT    NOT NULL CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    -- Single-box kinds (all but Opening): denormalized from the lines for the history list.
    cash_box_id       TEXT    REFERENCES cash_boxes (id),
    envelope_id       TEXT    REFERENCES envelopes (id),
    to_envelope_id    TEXT    REFERENCES envelopes (id),
    amount_cents      INTEGER CHECK (amount_cents IS NULL OR amount_cents > 0),
    -- TaxPayment: what was paid ("ONAT noviembre") and the receipt number.
    concept           TEXT,
    receipt           TEXT,
    notes             TEXT,
    status            TEXT    NOT NULL CHECK (status IN ('Confirmed', 'Voided')),
    void_reason       TEXT,
    voided_at         TEXT,
    created_at        TEXT    NOT NULL,
    updated_at        TEXT    NOT NULL,
    CHECK ((status = 'Voided') = (void_reason IS NOT NULL)),
    CHECK (to_envelope_id IS NULL OR to_envelope_id <> envelope_id)
) STRICT;

CREATE INDEX ix_envelope_operations_date ON envelope_operations (date);

-- What each operation did to each (cash box, envelope). One cash movement per line with a non-zero amount.
CREATE TABLE envelope_operation_lines (
    id            TEXT    NOT NULL PRIMARY KEY,
    operation_id  TEXT    NOT NULL REFERENCES envelope_operations (id),
    line_no       INTEGER NOT NULL,
    cash_box_id   TEXT    NOT NULL REFERENCES cash_boxes (id),
    envelope_id   TEXT    NOT NULL REFERENCES envelopes (id),
    -- Signed, in the box's currency. Opening: target − balance it had (0 = it already matched).
    amount_cents  INTEGER NOT NULL,
    -- Opening only: what was physically there.
    target_cents  INTEGER,
    UNIQUE (operation_id, line_no),
    UNIQUE (operation_id, cash_box_id, envelope_id)
) STRICT;

-- The cash ledger learns the envelope of each row, and the documents of the new rows.
-- envelope_id stays nullable in the schema (SQLite cannot add a NOT NULL foreign key), but the seeder fills the
-- old rows and every writer sets it.
ALTER TABLE cash_movements ADD COLUMN envelope_id TEXT REFERENCES envelopes (id);
ALTER TABLE cash_movements ADD COLUMN envelope_operation_id TEXT REFERENCES envelope_operations (id);
ALTER TABLE cash_movements ADD COLUMN stock_operation_id TEXT REFERENCES stock_operations (id);

CREATE INDEX ix_cash_movements_envelope ON cash_movements (envelope_id, cash_box_id, date);
CREATE INDEX ix_cash_movements_envelope_operation ON cash_movements (envelope_operation_id);
CREATE INDEX ix_cash_movements_stock_operation ON cash_movements (stock_operation_id);
