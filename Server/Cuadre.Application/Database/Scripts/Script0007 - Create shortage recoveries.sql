-- Slice 6b: a shortage is claimed and recovered (in parts) or written off (RF-CIE-13, RN-06c).
-- A recovery is money that comes in the day it is recovered, split like the A3 of that day. Never edited: voided.

CREATE TABLE cash_discrepancy_recoveries (
    id             TEXT    NOT NULL PRIMARY KEY,
    discrepancy_id TEXT    NOT NULL REFERENCES cash_discrepancies (id),
    date           TEXT    NOT NULL CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    cash_box_id    TEXT    NOT NULL REFERENCES cash_boxes (id),
    amount_cents   INTEGER NOT NULL CHECK (amount_cents > 0),
    note           TEXT,
    status         TEXT    NOT NULL CHECK (status IN ('Confirmed', 'Voided')),
    void_reason    TEXT,
    voided_at      TEXT,
    created_at     TEXT    NOT NULL,
    updated_at     TEXT    NOT NULL
) STRICT;

CREATE INDEX ix_cash_discrepancy_recoveries_discrepancy ON cash_discrepancy_recoveries (discrepancy_id);
CREATE INDEX ix_cash_discrepancy_recoveries_date ON cash_discrepancy_recoveries (date);

-- Written off: why and when. The status gains Recovered and Lost (stored by name, no CHECK on purpose).
ALTER TABLE cash_discrepancies ADD COLUMN resolution_note TEXT;
ALTER TABLE cash_discrepancies ADD COLUMN resolved_at TEXT;

-- The cash ledger learns the recovery behind its rows.
ALTER TABLE cash_movements ADD COLUMN recovery_id TEXT REFERENCES cash_discrepancy_recoveries (id);
CREATE INDEX ix_cash_movements_recovery ON cash_movements (recovery_id);
